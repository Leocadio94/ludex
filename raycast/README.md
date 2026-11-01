# Ludex for Raycast

The Raycast version of [Ludex](../README.md): browse your installed games and quickly open their save or config folders. Unlike the Flow Launcher and Command Palette versions, this one is TypeScript and shares no code with the C# core. Powered by the [Ludusavi Manifest](https://github.com/mtkennerly/ludusavi-manifest) — a community-maintained database of save locations for ~19,000 games.

## Features

### Browse Games
Lists all games currently detected on your machine, grouped by store. For each game, it shows how many save and config paths are known, whether those paths exist on disk, and lets you open them directly in Finder (macOS) or Explorer (Windows).

- Cmd+R — Refresh installed games
- Cmd+Shift+R — Refresh full game database

### Search Games
Searches the entire Ludusavi database by name, useful for looking up whether a game is supported before installing, or for finding its save location manually.

### Refresh Installed Games
Re-scans all stores to detect newly installed or removed games and updates the local cache. The installed games list is cached for 1 hour; use this command to bust that cache immediately.

### Refresh Game Database
Re-downloads and re-indexes the full Ludusavi Manifest from GitHub. Use this if game save paths seem outdated. The database is cached locally for 7 days.

## Supported Stores

| Store | Detection method |
|-------|-----------------|
| Steam | Registry (Windows) / default path (macOS) + `libraryfolders.vdf` |
| GOG | Registry (Windows) / GOG Galaxy config (macOS) |
| Epic Games | Launcher manifests (`%ProgramData%\Epic\...`) |
| Xbox / Game Pass | `%LOCALAPPDATA%\Packages\...` |

## How it works

1. On first launch, the Ludusavi Manifest (~16 MB YAML, ~19,000 entries) is downloaded and streamed directly to disk — no large in-memory buffers.
2. The manifest is parsed line-by-line and indexed into a compact JSON file, filtered to the current OS to reduce size. This index is stored in Raycast's support directory and reused for 7 days.
3. Installed games are detected by reading each store's native metadata files (`.acf` for Steam, registry/config for GOG, JSON manifests for Epic, package folders for Xbox).
4. Detected games are matched against the index by store ID first, then by normalized name as a fallback.
5. Matched save/config paths are resolved by expanding Ludusavi path variables (`<home>`, `<winAppData>`, `<winLocalAppData>`, etc.) and checking existence on disk.

## Path variables

The Ludusavi Manifest uses named placeholders for common OS directories. This extension resolves them to real paths at runtime:

| Variable | Windows | macOS |
|----------|---------|-------|
| `<home>` | `C:\Users\<user>` | `/Users/<user>` |
| `<winAppData>` | `%APPDATA%` | — |
| `<winLocalAppData>` | `%LOCALAPPDATA%` | — |
| `<winLocalAppDataLow>` | `AppData\LocalLow` | — |
| `<winDocuments>` | `Documents` | — |
| `<winProgramData>` | `%PROGRAMDATA%` | — |
| `<xdgData>` | — | `~/.local/share` |
| `<xdgConfig>` | — | `~/.config` |
| `<base>` | Game install directory | Game install directory |

## Development

```bash
npm install
npm run dev     # live-reload in Raycast
npm run lint
npm run build
```

## Credits

Save path data is provided by the [Ludusavi Manifest](https://github.com/mtkennerly/ludusavi-manifest), maintained by the open-source community.
