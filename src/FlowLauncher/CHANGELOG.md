# Changelog

Flow Launcher plugin. Format: [Keep a Changelog](https://keepachangelog.com). The version is `Version` in
`plugin.json`.

## [Unreleased]

### Fixed
- A save path with `<storeGameId>` no longer lists other games' folders (such as Steam's `userdata`); the path uses the detected store ID and is skipped when there is none.

## [1.0.0] - 2026-11-03

First public release. The plugin existed before under the name GameSavesManager (keyword `gs`) and was never
published; it was renamed to Ludex (keyword `lx`) and keeps its plugin ID.

### Added
- Installed games from Steam, GOG, Epic Games and Xbox, with save, config and install folders from the Ludusavi manifest.
- Launch Game, `!all` search across the whole database, `!games` and `!refresh` commands.
- Game icons from store artwork, one row per folder group, and a nearest-parent fallback for missing folders.
- Default sort order setting.
- Brazilian Portuguese, following Flow's own language setting and applied live.
- Refresh progress and result shown as rows in the game list; the progress dialog only appears for slow refreshes.

### Changed
- Detection, manifest handling and path resolution moved to the shared `Ludex.Core`.
