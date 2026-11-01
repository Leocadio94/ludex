# Ludex

Every PC game hides its saves somewhere different: `AppData\Roaming`, `AppData\LocalLow`,
`Documents\My Games`, the Steam Cloud folder, or right next to the executable. Ludex lists the
games installed on your machine, opens the right save, config or install folder for each one, and
launches them, straight from your launcher.

Save locations come from the [Ludusavi manifest](https://github.com/mtkennerly/ludusavi-manifest),
a community-maintained database covering essentially every PC game.

Stores detected: **Steam**, **GOG**, **Epic Games**, **Xbox / Game Pass**.

## Versions

| Launcher | Language | Status | Code | Docs |
|---|---|---|---|---|
| [Flow Launcher](https://www.flowlauncher.com) | C# | usable, from source | [`src/FlowLauncher`](src/FlowLauncher) + [`src/Core`](src/Core) | [README](src/FlowLauncher/README.md) |
| [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) | C# | in progress | `src/CommandPalette` + [`src/Core`](src/Core) | |
| [Raycast](https://raycast.com) | TypeScript | usable, from source | [`raycast`](raycast) | [README](raycast/README.md) |

The Flow Launcher and Command Palette versions are thin front ends over the same C# library,
`Ludex.Core`: store detection, manifest download and indexing, matching and path resolution. The
Raycast version is a separate TypeScript implementation of the same idea and shares no code with
them, so behaviour can differ in the details.

## Repository layout

```
src/Core            Ludex.Core: detection, manifest, matcher, path resolver
src/FlowLauncher    Flow Launcher plugin
src/CommandPalette  PowerToys Command Palette extension
raycast             Raycast extension (TypeScript, independent)
scripts             deploy and icon generation for the Flow plugin
```

## Building

```powershell
dotnet build Ludex.sln -c Release
.\scripts\deploy.ps1      # installs the Flow Launcher plugin and restarts Flow
```

Requires the .NET 9 SDK. The Raycast extension has its own toolchain: see
[`raycast/README.md`](raycast/README.md).

## Credits

Ludex knows nothing about games on its own. Everything it shows comes from the
[**Ludusavi manifest**](https://github.com/mtkennerly/ludusavi-manifest) by
[Matthew T. Kennerly](https://github.com/mtkennerly), an open-source, community-maintained
database of save and config locations, distributed under the MIT licence.

The manifest is in turn compiled from [**PCGamingWiki**](https://www.pcgamingwiki.com), whose
contributors document where each game keeps its saves. PCGamingWiki content is licensed
[CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/).

**If a game's paths are wrong or missing, fix it at the source.** Edit the article on
PCGamingWiki and the correction flows into the manifest, and from there into Ludex on the next
refresh. Opening an issue here cannot fix the data.

## Licence

[MIT](LICENSE) © 2026 Zeocadio.

The game database is not redistributed with Ludex; it is downloaded from the Ludusavi manifest
repository at runtime and cached locally.
