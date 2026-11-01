import { Action, ActionPanel, Icon, List, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { loadManifest, refreshManifest } from "./lib/manifest";
import type { IndexedGame, ManifestIndex } from "./lib/types";

export default function Command() {
  const [manifest, setManifest] = useState<ManifestIndex | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [statusMsg, setStatusMsg] = useState("Loading game database...");

  async function load(forceRefresh = false) {
    setIsLoading(true);
    try {
      const index = forceRefresh
        ? await refreshManifest(setStatusMsg)
        : await loadManifest(setStatusMsg);
      setManifest(index);
    } catch (err) {
      await showToast({
        style: Toast.Style.Failure,
        title: "Failed to load manifest",
        message: String(err),
      });
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  const entries: [string, IndexedGame][] = manifest ? Object.entries(manifest) : [];

  return (
    <List
      isLoading={isLoading}
      searchBarPlaceholder="Search all games..."
      navigationTitle={
        isLoading ? statusMsg : `${entries.length.toLocaleString()} games in database`
      }
    >
      {entries.map(([name, game]) => (
        <GameEntry
          key={name}
          name={name}
          game={game}
          onRefresh={() => load(true)}
        />
      ))}
    </List>
  );
}

function GameEntry({
  name,
  game,
  onRefresh,
}: {
  name: string;
  game: IndexedGame;
  onRefresh: () => void;
}) {
  const accessories: List.Item.Accessory[] = [];

  if (game.steamId) accessories.push({ text: `Steam: ${game.steamId}`, icon: Icon.Store });
  if (game.gogId) accessories.push({ text: `GOG: ${game.gogId}`, icon: Icon.Store });

  const pathCount = game.files.length;
  accessories.push({
    text: `${pathCount} path${pathCount !== 1 ? "s" : ""}`,
    icon: Icon.Folder,
  });

  return (
    <List.Item
      title={name}
      accessories={accessories}
      actions={
        <ActionPanel>
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
