# Ludex for Raycast

Windows only.

The Raycast version of [Ludex](https://github.com/Leocadio94/ludex). It lists your installed games and opens their save, config or install folders, or launches the game. Unlike the Flow Launcher and Command Palette versions, this one is written in TypeScript and shares no code with the C# core. Save locations come from the [Ludusavi Manifest](https://github.com/mtkennerly/ludusavi-manifest), a community-maintained database covering about 19,000 games.

## Features

### Browse Games

Lists the games detected on your machine, grouped by store, each with the artwork its store keeps on disk. Each game shows how many save and config paths are known and whether they exist on disk. From there you can open a folder in Explorer or launch the game. If a save folder has not been created yet, the nearest existing parent opens instead.

- Ctrl+R refreshes the installed games.
- Ctrl+Shift+R refreshes the game database.

### Search Games

Searches the whole Ludusavi database by name. Use it to check whether a game is supported before you install it, or to find a save location by hand.

### Refresh Installed Games

Re-scans the stores for newly installed or removed games and updates the local cache. The installed games list is cached for 1 hour, and this command clears it right away.

### Refresh Game Database

Downloads the Ludusavi Manifest from GitHub again and rebuilds the index. Use it when save paths look outdated. The database is cached locally for 7 days.

## Supported Stores

| Store            | Detection method                              |
| ---------------- | --------------------------------------------- |
| Steam            | Registry + `libraryfolders.vdf`               |
| GOG              | Registry                                      |
| Epic Games       | Launcher manifests (`%ProgramData%\Epic\...`) |
| Xbox / Game Pass | `%LOCALAPPDATA%\Packages\...`                 |

## How it works

1. On first launch, the Ludusavi Manifest (about 16 MB of YAML, roughly 19,000 entries) is streamed straight to disk, so it never sits in memory.
2. The manifest is parsed line by line into a compact JSON index, filtered to the current OS to keep it small. The index lives in Raycast's support directory and is reused for 7 days.
3. Installed games are found by reading each store's own files: `.acf` for Steam, the registry for GOG, JSON manifests for Epic and package folders for Xbox.
4. Detected games are matched against the index by store ID first, then by normalized name.
5. Save and config paths are resolved by expanding the Ludusavi path variables (`<home>`, `<winAppData>`, `<winLocalAppData>` and so on) and checking whether they exist on disk.

## Path variables

The Ludusavi Manifest uses named placeholders for common directories. The extension resolves them to real paths at runtime:

| Variable               | Resolves to            |
| ---------------------- | ---------------------- |
| `<home>`               | `C:\Users\<user>`      |
| `<winAppData>`         | `%APPDATA%`            |
| `<winLocalAppData>`    | `%LOCALAPPDATA%`       |
| `<winLocalAppDataLow>` | `AppData\LocalLow`     |
| `<winDocuments>`       | `Documents`            |
| `<winProgramData>`     | `%PROGRAMDATA%`        |
| `<base>`               | Game install directory |

## Development

```bash
npm install
npm run dev     # live-reload in Raycast
npm run lint
npm run build
```

## Credits

Save path data is provided by the [Ludusavi Manifest](https://github.com/mtkennerly/ludusavi-manifest), maintained by the open-source community.
