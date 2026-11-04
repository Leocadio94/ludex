# Changelog

PowerToys Command Palette extension. Format: [Keep a Changelog](https://keepachangelog.com).

## [Unreleased]

### Added
- First version: installed games with a details pane, per-game folders, Launch Game, search across the whole
  database, and refresh commands with progress shown in a page.
- Brazilian Portuguese, following the Windows display language (run `Reload` after changing it).
- Ludex icon on the extension executable.

### Fixed
- A save path with `<storeGameId>` no longer lists other games' folders (such as Steam's `userdata`); the path uses the detected store ID and is skipped when there is none.
