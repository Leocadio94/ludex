# Changelog

All notable changes to Ludex as a whole. Each launcher also keeps its own log:
[Flow Launcher](src/FlowLauncher/CHANGELOG.md), [Command Palette](src/CommandPalette/CHANGELOG.md),
[Raycast](raycast/CHANGELOG.md). Format: [Keep a Changelog](https://keepachangelog.com).

## [Unreleased]

### Added
- Brazilian Portuguese for the Flow Launcher plugin and the Command Palette extension, plus `README.pt-BR.md`.
- Launch Game in all three launchers.
- Game icons in Raycast, read from the artwork each store keeps on disk.
- Open Install Folder in Raycast.
- Shared `Ludex.Core` library for Flow Launcher and Command Palette.
- Command Palette extension.
- Logo, banners and generated launcher icons.
- Documentation in `docs/`: architecture, decisions, localization, Command Palette setup, publishing notes.
- "Build from source" instructions in the READMEs.

### Changed
- Project renamed from GameSavesManager to Ludex (Flow keyword `gs` became `lx`); the repository is now a monorepo.
- Raycast version is restricted to Windows.

### Fixed
- `<root>` in manifest paths now resolves to the store root (the folder containing `steamapps`).
- Search Games in Raycast no longer renders the whole database.
- Raycast lists games whose save folder does not exist yet (wildcard paths now fall back to the parent folder, as in Flow and Command Palette), and opens the nearest existing folder.
- Refresh progress and result are shown inside the Flow Launcher list instead of a dialog that stole focus.
- Raycast: a Steam save path with `<storeGameId>` no longer lists other games' folders; a failed launch, load or download shows an error with a retry; the last database keeps working offline; refreshes no longer write over each other, and refreshing an open list shows its progress in a toast instead of re-rendering the list.
