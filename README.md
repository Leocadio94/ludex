# Game Saves Manager — Flow Launcher plugin

Every PC game hides its saves somewhere different: `AppData\Roaming`, `AppData\LocalLow`,
`Documents\My Games`, the Steam Cloud folder, or right next to the executable. This plugin
lists the games installed on your machine and opens the right folder for you.

Save locations come from the [Ludusavi manifest](https://github.com/mtkennerly/ludusavi-manifest),
a community-maintained database covering essentially every PC game — the same data as
PCGamingWiki, in a form software can actually read.

## Usage

| Query | What it does |
|---|---|
| `gs` | List every installed game |
| `gs witcher` | Filter by name |
| `gs !games` | Re-scan the stores for newly installed games |
| `gs !refresh` | Re-download the game database |
| `gs !all doom` | Search the whole database, including games you have not installed |

Press <kbd>Enter</kbd> on a game to see its folders — save, config and install. Press
<kbd>Enter</kbd> again to open one, or <kbd>Ctrl</kbd>+<kbd>O</kbd> to copy its path instead.

Stores detected: **Steam**, **GOG**, **Epic Games**, **Xbox / Game Pass**.

Games that exist in your library but not in the database are still listed, with their install
folder — turn that off in the plugin settings if you'd rather not see them.

## Installation

Not yet in the Flow Launcher plugin store. To install from source:

```powershell
git clone https://github.com/Zeocadio/Flow.Launcher.Plugin.GameSavesManager
cd Flow.Launcher.Plugin.GameSavesManager
.\scripts\deploy.ps1
```

Requires the .NET 9 SDK. The script builds the plugin, installs it into
`%APPDATA%\FlowLauncher\Plugins`, and restarts Flow Launcher.

The first query downloads and indexes the database (~17 MB, a few seconds). After that
everything is served from memory; the database refreshes itself every 7 days.

## Settings

Which stores to scan, whether to show games missing from the database, result limit, and both
cache lifetimes. Flow Launcher → Settings → Plugins → Game Saves Manager.

## Related

A [Raycast](https://raycast.com) version of this plugin exists for macOS and Windows. This one
is an independent implementation for Flow Launcher, not a wrapper around it.

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

## Licence

[MIT](LICENSE) — © 2026 Zeocadio.

The bundled game database is not redistributed with this plugin; it is downloaded from the
Ludusavi manifest repository at runtime and cached locally.
