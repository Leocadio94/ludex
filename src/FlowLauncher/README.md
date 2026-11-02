# Ludex for Flow Launcher

Every PC game hides its saves somewhere different: `AppData\Roaming`, `AppData\LocalLow`,
`Documents\My Games`, the Steam Cloud folder, or right next to the executable. This plugin
lists the games installed on your machine, opens the right folder for each one, and can launch
them — all from Flow Launcher.

Save locations come from the [Ludusavi manifest](https://github.com/mtkennerly/ludusavi-manifest),
a community-maintained database covering essentially every PC game — the same data as
PCGamingWiki, in a form software can actually read.

Stores detected: **Steam**, **GOG**, **Epic Games**, **Xbox / Game Pass**.

## Usage

| Query | What it does |
|---|---|
| `lx` | List every installed game |
| `lx witcher` | Filter the installed games by name |
| `lx !games` | Re-scan the stores for newly installed games |
| `lx !refresh` | Re-download and re-index the game database |
| `lx !all doom` | Search the whole database, including games you have not installed |

Press <kbd>Enter</kbd> on a game to open its actions — launch it, or open its save, config and
install folders. Press <kbd>Enter</kbd> again to act. On a folder, <kbd>Ctrl</kbd>+<kbd>O</kbd>
opens the context menu to copy its path instead of opening it.

### What each action does

- **Launch Game** — starts the game through the store it came from: Steam and the Epic
  launcher by protocol, the Xbox app through the shell, and GOG titles by running the
  executable directly, so they start whether or not GOG Galaxy is running.
- **Open Save / Config Folder** — opens the folder the game keeps its data in. When several
  save files live in the same folder, they collapse into one entry so you don't see the same
  destination twice.
- **Open Install Folder** — the game's own directory, even for games missing from the database.

### Nice touches

- Games show their own **store artwork** instead of a generic icon.
- A save folder that **doesn't exist yet** — normal for a game you've never launched — says so,
  and opening it takes you to the nearest folder that does exist, where it will appear.
- Refreshing shows a **progress bar** while it downloads and indexes.
- Games in your library but **missing from the database** are still listed with their install
  folder (toggleable — see Settings).

## Settings

Flow Launcher → Settings → Plugins → Ludex.

| Setting | Default | What it does |
|---|---|---|
| **Stores to scan** | all on | Turn Steam, GOG, Epic or Xbox detection on or off individually. |
| **Default order for `lx`** | Most used first | `Most used first` lets Flow float the games you open often to the top. `Alphabetical (A-Z)` gives a fixed order that ignores usage. Only affects the plain `lx` list — searching always ranks by relevance. |
| **Show games with no manifest entry** | on | List installed games the database doesn't know about, with just their install folder. Off hides them. |
| **Maximum results** | 50 | Cap on how many games the list shows at once. |
| **Game database refresh interval (days)** | 7 | How often the Ludusavi manifest is re-downloaded automatically. `lx !refresh` forces it immediately. |
| **Installed games cache lifetime (minutes)** | 60 | How long the detected-games list is reused before re-scanning the stores. `lx !games` forces a re-scan immediately. |

## Installation

Not yet in the Flow Launcher plugin store. To install from source:

```powershell
git clone https://github.com/Leocadio94/ludex
cd ludex
.\scripts\deploy.ps1
```

Requires the .NET 9 SDK. The script builds the plugin, installs it into
`%APPDATA%\FlowLauncher\Plugins`, and restarts Flow Launcher.

The first query downloads and indexes the database (~17 MB, a few seconds). After that
everything is served from memory; the database refreshes itself on the interval above.

## How it works

1. **Detect** — read each store's own metadata (Steam `.acf` files, the GOG and Xbox
   registries, Epic's JSON manifests). No PowerShell, no store clients need to be running.
2. **Index** — download the Ludusavi manifest and parse it into a compact on-disk index,
   filtered to the current OS.
3. **Match** — join detected games to the manifest by store ID, then by name.
4. **Resolve** — turn manifest path templates (`<winAppData>`, `<home>`, `<base>`…) into real
   folders on your machine, expanding wildcards against the filesystem.

Everything is cached; the whole pipeline runs once per Flow session and then serves from
memory.

## Related

Ludex also ships for [Raycast](../../raycast/README.md) and, in progress, for the PowerToys
Command Palette. The detection and path-resolution code in this plugin lives in
[`src/Core`](../Core) and is shared with the Command Palette version; the Raycast version is an
independent TypeScript implementation. See the [repository README](../../README.md).

## Credits

This plugin knows nothing about games on its own. Everything it shows comes from the
[**Ludusavi manifest**](https://github.com/mtkennerly/ludusavi-manifest) by
[Matthew T. Kennerly](https://github.com/mtkennerly) — an open-source, community-maintained
database of save and config locations for essentially every PC game, distributed under the
MIT licence.

The manifest is in turn compiled from [**PCGamingWiki**](https://www.pcgamingwiki.com), whose
contributors are the people actually documenting where each game hides its saves. PCGamingWiki
content is licensed [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/).

**If a game's paths are wrong or missing, fix it at the source** — edit the article on
PCGamingWiki and the correction flows into the manifest, and from there into this plugin (and
every other tool built on it) on the next refresh. Opening an issue here cannot fix the data.

Built for [Flow Launcher](https://www.flowlauncher.com), which is itself open source.

## Language

English by default; Brazilian Portuguese when Flow's language is set to it (Settings > General > Language).
How it works and how to add text: [docs/localization.md](../../docs/localization.md).

## Licence

[MIT](../../LICENSE) — © 2026 Gabriel Leocádio.

The bundled game database is not redistributed with this plugin; it is downloaded from the
Ludusavi manifest repository at runtime and cached locally.
