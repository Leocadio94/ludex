# Changelog

All notable changes to Ludex as a whole. Each launcher also keeps its own log:
[Flow Launcher](src/FlowLauncher/CHANGELOG.md), [Command Palette](src/CommandPalette/CHANGELOG.md),
[Raycast](raycast/CHANGELOG.md). Format: [Keep a Changelog](https://keepachangelog.com).

## [Unreleased]

### Added
- Brazilian Portuguese for the Flow Launcher plugin and the Command Palette extension, plus `README.pt-BR.md`.
- Launch Game in all three launchers.
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
- Refresh progress and result are shown inside the Flow Launcher list instead of a dialog that stole focus.
