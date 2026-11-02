# Changelog

Flow Launcher plugin. Format: [Keep a Changelog](https://keepachangelog.com). The version is `Version` in
`plugin.json`.

## [Unreleased]

### Added
- Brazilian Portuguese, following Flow's own language setting and applied live.
- Refresh progress and result shown as rows in the game list; the progress dialog only appears for slow refreshes.

### Changed
- Keyword changed from `gs` to `lx`; the plugin ID is unchanged, so existing settings carry over.
- Detection, manifest handling and path resolution moved to the shared `Ludex.Core`.

### Fixed
- The refresh dialog no longer steals focus and hides Flow.

## [1.0.0]

### Added
- Installed games from Steam, GOG, Epic Games and Xbox, with save, config and install folders from the Ludusavi manifest.
- Launch Game, `!all` search across the whole database, `!games` and `!refresh` commands.
- Game icons from store artwork, one row per folder group, and a nearest-parent fallback for missing folders.
- Default sort order setting.
