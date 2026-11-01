# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
dotnet build Ludex.sln -c Release    # compile Core + Flow plugin
.\scripts\deploy.ps1             # publish + install into the local Flow Launcher, restart it
.\scripts\deploy.ps1 -NoRestart  # same, but leave Flow closed
.\scripts\make-icons.ps1         # regenerate Images/*.png from Segoe Fluent Icons glyphs
```

Flow Launcher holds the plugin DLL open, so `deploy.ps1` stops the process before copying.
Runtime errors land in `%APPDATA%\FlowLauncher\Logs\<flow-version>\`.

## Architecture

A [Flow Launcher](https://www.flowlauncher.com) C# plugin. Pipeline:
**detect installed games → load manifest index → match → resolve paths → display**.

The plugin is in-process and persistent: the manifest index is loaded once per Flow session
and stays in `Main._index`, so typing in the query box does no I/O.

### Data source

The [Ludusavi manifest](https://github.com/mtkennerly/ludusavi-manifest) — a single YAML
file (~17 MB, ~52k entries) describing where every PC game keeps its saves and config. It is
the machine-readable equivalent of PCGamingWiki, which has no usable API.

### Entry point (`src/FlowLauncher/Main.cs`)

`IAsyncPlugin`, `IContextMenu`, `ISettingProvider`, `IAsyncReloadable`.

`InitAsync` blocks Flow's startup, so it only wires things up and kicks off `StartLoad` in the
background. `QueryAsync` waits up to 2.5 s on that task and otherwise shows a status line.

Query routing on `Query.Search`:

| Input | Handler |
|---|---|
| `lx` / `lx <text>` | `GameResults` — installed games, filtered with `API.FuzzySearch` |
| `lx >{slug}` | `DrillInResults` — the folder list for one game |
| `lx !games` / `lx !refresh` | `CommandResults` → `RunRefresh` |
| `lx !all <text>` | `SearchAllResults` — the whole manifest, installed or not |

Enter on a game rewrites the query to `lx >{slug} ` via `API.ChangeQuery` and returns `false`
so the window stays open. Slugs come from `Matcher.MakeSlug` and are guaranteed unique and
space-free.

The empty-query list has two orderings, chosen by `Settings.DefaultSort`. **Usage** (default)
gives every game the same score and leaves `Result.AddSelectedCount` on, so Flow's own usage
history floats frequently-opened games up. **Alphabetical** assigns strictly descending scores
by A-Z position and sets `AddSelectedCount = false`, otherwise Flow re-sorts equal scores by
usage and the alphabetical order is lost. Searching (`lx <text>`) always ranks by fuzzy score
regardless of the setting.

Two more behaviours exist because the obvious implementation left the user staring at a
launcher that appeared to do nothing:

- **Folders that do not exist yet.** A game that has never been launched has no save folder,
  which is normal, not an error. `OpenFolder` walks up to the nearest existing ancestor and
  opens that instead; `FolderResult` says so in the subtitle before the user presses Enter.
- **Repeated rows.** `BuildEntries` groups by the directory Enter will actually open, not by
  manifest path. The manifest commonly lists several files inside one folder — Crimson Desert
  has three save entries in the same directory — and opening a file opens its parent, so
  without grouping the same destination appears several times. `Disambiguate` then appends the
  folder name to any titles that still collide.
- **Refreshes.** `RunRefreshAsync` drives `API.ShowProgressBoxAsync`, with progress threaded
  from `HttpDownloadAsync` through indexing (0-60 download, 60-85 index, 85-100 detect and
  match). It is called directly from the result action so the dialog is created on the UI
  thread, and the returned task is deliberately not awaited.

### Core library (`src/Core/`, namespace `Ludex.Core`)

**`ManifestStore.cs`** — download → index → load.
The YAML is parsed line by line against its indentation rather than with a YAML library, and
entries are written to `manifest-index.json` one at a time through a `Utf8JsonWriter`, so the
document is never fully materialized. ~1 s for the whole file. Index property names are
single letters because the file holds ~14.5k entries.

Alias-only manifest entries (`alias: Other Game`, ~1000 of them) are kept with no files;
`Matcher.FollowAlias` redirects them to the canonical entry.

**`PathResolver.cs`** — substitutes manifest placeholders (`<winAppData>`, `<home>`,
`<base>`…) and expands `*` segments against the filesystem.

Two behaviours that matter:
- `<root>` is the **store root** (the folder containing `steamapps`), not the install
  directory's parent. Templates like `<root>/userdata/<storeUserId>/…` depend on it, which is
  why `InstalledGame.StoreRoot` exists.
- When a wildcard in the **final** segment matches nothing, the concrete parent is returned
  instead — the save folder is still what the user wants even before the game writes to it.
  Wildcards in the middle of a path get no fallback, or a shared root like Steam's `userdata`
  would be surfaced for every game.

**`Matcher.cs`** — store ID first (Steam appid → `steamId`, GOG productId → `gogId`), then
normalized-name fallback. Filters each file entry by OS and by `StoreAliases` (Xbox games
appear as `microsoft` in the manifest). Games with no manifest match are kept when
`Settings.ShowUnmatchedGames` is on, carrying only their install folder.

**`GameDetector.cs`** — runs enabled detectors in parallel; a store that throws is logged and
dropped rather than failing the rest. Result cached to `installed-games.json`, 1 h TTL.

**`Detectors/`** — one per store, all registry- or file-based, no PowerShell:
| Store | Source |
|---|---|
| Steam | `HKCU\SOFTWARE\Valve\Steam` → `libraryfolders.vdf` → `appmanifest_*.acf` (`Vdf.cs`) |
| GOG | `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games` |
| Epic | `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` |
| Xbox | AppModel package registry filtered to packages carrying `MicrosoftGame.config`, merged by name with `<drive>:\XboxGames\*\Content` |

Xbox is the one worth knowing about: the obvious implementation shells out to
`Get-AppxPackage`, which takes tens of seconds. The registry path runs in well under one
second and needs no Windows SDK reference.

Each detector also resolves `InstalledGame.IconPath` from artwork the store already put on
disk — no network calls, no icon extraction of our own:
| Store | Icon source |
|---|---|
| Steam | `appcache\librarycache\<appid>\` — the client icon is stored under its content hash, so it is found by shape (40 hex chars), falling back to `logo.png` / `header.jpg` |
| GOG | `goggame-<productid>.ico` in the install folder |
| Epic | the `LaunchExecutable` from the .item manifest — Flow extracts the embedded icon itself |
| Xbox | `AppxManifest.xml` → `VisualElements/@Square150x150Logo`, resolved through its `.scale-*` variants; `Properties/Logo` is only a 50x50 store logo |

They also resolve `InstalledGame.LaunchTarget`, consumed by `GameLauncher`:
| Store | Launch target |
|---|---|
| Steam | `steam://rungameid/<appid>` |
| GOG | primary `FileTask` from `goggame-<id>.info` — runs without Galaxy |
| Epic | `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true` — keyed on `AppName`, not the `CatalogItemId` used for matching |
| Xbox | `shell:appsFolder\<PackageFamilyName>!<AppId>`, family name derived from the package full name by keeping the first and last underscore-separated segments |

`shell:` targets go through `explorer.exe`; ShellExecute does not understand them directly.

`installed-games-v3.json` carries a schema version in its name. Bump it whenever
`InstalledGame` gains a field, or existing caches will serve nulls for it until the TTL
expires.

### Injected dependencies

`ManifestStore` and `GameDetector` take delegates (`download`, `persistSettings`, `logError`)
rather than `IPublicAPI`. That keeps the whole pipeline runnable outside Flow — useful,
because the alternative is debugging a parser through a launcher UI.

## Relationship to the Raycast version

Ported from the Raycast extension at `raycast/`, which shares the
pipeline but not the code. Differences that were deliberate:

- Install folder is exposed (the Raycast matcher drops `installPath` after matching).
- Unmatched games are listed instead of discarded.
- `<root>` is resolved correctly; the Raycast version uses `dirname(installPath)`.
- Manifest `alias` entries are used; the Raycast version parses them into a type it never fills.
- The V8 heap limit that forced the streaming design there does not apply here, but the
  streaming parser was kept because it is also the fastest option.
