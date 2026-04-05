import {
  Action,
  ActionPanel,
  Color,
  Icon,
  List,
  open,
  showToast,
  Toast,
} from "@raycast/api";
import { useEffect, useState } from "react";
import { detectInstalledGames } from "./lib/game-detector";
import { loadManifest, refreshManifest } from "./lib/manifest";
import { matchGames } from "./lib/matcher";
import type { GameWithPaths, ResolvedPath, Store } from "./lib/types";

const STORE_LABEL: Record<Store, string> = {
  steam: "Steam",
  gog: "GOG",
  epic: "Epic Games",
  xbox: "Xbox",
};

const STORE_ORDER: Store[] = ["steam", "gog", "epic", "xbox"];

export default function Command() {
  const [games, setGames] = useState<GameWithPaths[]>([]);
  console.log("🚀 ~ browse-games.tsx:28 ~ Command ~ games:", games);
  const [isLoading, setIsLoading] = useState(true);
  const [statusMsg, setStatusMsg] = useState("Starting...");
  // const mountedRef = useRef(true);

  async function loadGames(forceRefresh = false) {
    setIsLoading(true);
    try {
      const manifest = forceRefresh
        ? await refreshManifest(setStatusMsg)
        : await loadManifest(setStatusMsg);

      // if (!mountedRef.current) return;
      setStatusMsg("Detecting installed games...");

      const installed = await detectInstalledGames();
      // console.log(
      //   "🚀 ~ browse-games.tsx:44 ~ loadGames ~ installed:",
      //   installed
      // );
      // console.log(
      //   "🚀 ~ browse-games.tsx:43 ~ loadGames ~ installed:",
      //   installed
      // );
      // if (!mountedRef.current) return;

      setStatusMsg("Matching against database...");
      const matched = matchGames(installed, manifest);

      // if (!mountedRef.current) return;
      setGames(matched);

      if (matched.length === 0) {
        await showToast({
          style: Toast.Style.Failure,
          title: "No games found",
          message: "Could not detect games with known save/config paths.",
        });
      }
    } catch (err) {
      // if (!mountedRef.current) return;
      await showToast({
        style: Toast.Style.Failure,
        title: "Error loading games",
        message: String(err),
      });
    } finally {
      // if (mountedRef.current) setIsLoading(false);
      setIsLoading(false);
    }
  }

  useEffect(() => {
    (async () => {
      await loadGames();
    })();

    // return () => {
    //   mountedRef.current = false;
    // };
  }, []);

  const byStore = STORE_ORDER.map((store) => ({
    store,
    games: games.filter((g) => g.store === store),
  })).filter((s) => s.games.length > 0);

  return (
    <List
      isLoading={isLoading}
      searchBarPlaceholder="Search games..."
      navigationTitle={
        isLoading
          ? statusMsg
          : `${games.length} game${games.length !== 1 ? "s" : ""} found`
      }
    >
      {byStore.map(({ store, games: storeGames }) => (
        <List.Section key={store} title={STORE_LABEL[store]}>
          {storeGames.map((game) => (
            <GameListItem
              key={`${game.store}-${game.storeGameId ?? game.name}`}
              game={game}
              onRefresh={() => loadGames(true)}
            />
          ))}
        </List.Section>
      ))}
    </List>
  );
}

function GameListItem({
  game,
  onRefresh,
}: {
  game: GameWithPaths;
  onRefresh: () => void;
}) {
  const saveCount = game.savePaths.length;
  const configCount = game.configPaths.length;
  const saveExists = game.savePaths.some((p) => p.exists);
  const configExists = game.configPaths.some((p) => p.exists);

  const accessories: List.Item.Accessory[] = [];
  if (saveCount > 0) {
    accessories.push({
      tag: {
        value: `${saveCount} save${saveCount > 1 ? "s" : ""}`,
        color: saveExists ? Color.Green : Color.SecondaryText,
      },
      tooltip: saveExists ? "Save folder found" : "Save folder not found yet",
    });
  }
  if (configCount > 0) {
    accessories.push({
      tag: {
        value: `${configCount} config${configCount > 1 ? "s" : ""}`,
        color: configExists ? Color.Blue : Color.SecondaryText,
      },
      tooltip: configExists
        ? "Config folder found"
        : "Config folder not found yet",
    });
  }

  return (
    <List.Item
      title={game.name}
      accessories={accessories}
      actions={
        <ActionPanel>
          <SaveActions paths={game.savePaths} />
          <ConfigActions paths={game.configPaths} />
          <ActionPanel.Section>
            <Action
              title="Refresh Game Database"
              icon={Icon.ArrowClockwise}
              shortcut={{ modifiers: ["cmd"], key: "r" }}
              onAction={onRefresh}
            />
          </ActionPanel.Section>
        </ActionPanel>
      }
    />
  );
}

function SaveActions({ paths }: { paths: ResolvedPath[] }) {
  if (paths.length === 0) return null;
  return (
    <ActionPanel.Section title="Save Files">
      {paths.map((p, i) => (
        <Action
          key={i}
          title={
            paths.length > 1 ? `Open Save Folder ${i + 1}` : "Open Save Folder"
          }
          icon={p.exists ? Icon.Folder : Icon.QuestionMark}
          onAction={() => {
            console.log(
              "🚀 ~ browse-games.tsx:173 ~ SaveActions ~ p.path:",
              p.path
            );
            open(p.path);
          }}
        />
      ))}
    </ActionPanel.Section>
  );
}

function ConfigActions({ paths }: { paths: ResolvedPath[] }) {
  if (paths.length === 0) return null;
  return (
    <ActionPanel.Section title="Config Files">
      {paths.map((p, i) => (
        <Action
          key={i}
          title={
            paths.length > 1
              ? `Open Config Folder ${i + 1}`
              : "Open Config Folder"
          }
          icon={p.exists ? Icon.Gear : Icon.QuestionMark}
          onAction={() => open(p.path)}
        />
      ))}
    </ActionPanel.Section>
  );
}
