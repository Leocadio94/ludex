# Ludex for PowerToys Command Palette

Browse your installed games, launch them, and open their save, config or install folders from
[Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview). It shares its
detection and path-resolution code with the Flow Launcher plugin through [`src/Core`](../Core).

## Commands

| Command | What it does |
|---|---|
| **Ludex** | Installed games, most used first (or A-Z). Enter opens a game; the details pane lists its paths. |
| **Ludex: Search all games** | Searches the whole Ludusavi database, installed or not. |
| **Ludex: Refresh installed games** | Re-scans Steam, GOG, Epic and Xbox. Opens a page: press Enter to start. |
| **Ludex: Refresh game database** | Re-downloads the Ludusavi manifest (~16 MB), then re-detects games. |

Inside a game: **Launch Game**, then one row per save, config and install folder. A folder that doesn't exist
yet opens the nearest parent that does. Use the context menu on a folder to copy its path.

## Settings

Command Palette > Settings > Extensions > Ludex: stores to scan, default order, whether to list games with no
manifest entry, the manifest refresh interval, the installed-games cache lifetime and the search result cap.
Changes to the stores or intervals apply on the next refresh.

## Typing `lx`

Command Palette gives extensions no default alias. Set one yourself: Settings > Extensions > Ludex, alias on the
"Ludex" command.

## Install

Not published yet. To run it from source see [docs/command-palette.md](../../docs/command-palette.md).

[MIT](../../LICENSE) © 2026 Gabriel Leocádio.
