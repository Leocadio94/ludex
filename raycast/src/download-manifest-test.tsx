import { List, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { loadManifest } from "./lib/manifest";
import type { ManifestIndex } from "./lib/types";

export default function Command() {
  const [manifest, setManifest] = useState<ManifestIndex | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [statusMsg, setStatusMsg] = useState("Downloading manifest...");

  useEffect(() => {
    loadManifest(setStatusMsg)
      .then(setManifest)
      .catch(async (err) => {
        await showToast({
          style: Toast.Style.Failure,
          title: "Failed to load manifest",
          message: String(err),
        });
      })
      .finally(() => setIsLoading(false));
  }, []);

  const entries = manifest ? Object.entries(manifest) : [];

  return (
    <List
      isLoading={isLoading}
      navigationTitle={isLoading ? statusMsg : `${entries.length} games in manifest`}
    >
      {entries.map(([name, game]) => (
        <List.Item
          key={name}
          title={name}
          subtitle={`${game.files.length} path${game.files.length !== 1 ? "s" : ""}`}
          accessories={[
            ...(game.steamId ? [{ text: `Steam: ${game.steamId}` }] : []),
            ...(game.gogId ? [{ text: `GOG: ${game.gogId}` }] : []),
          ]}
        />
      ))}
    </List>
  );
}
