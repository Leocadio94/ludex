# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **Translations (PT-BR)**: check which front ends can localise their own text and translate those; keep English as
  the default everywhere else. Starting points, none verified yet: Flow plugins can ship `Languages/*.xaml`
  resource files; Command Palette extensions are packaged apps, so resources (`.resw`) may apply; Raycast has no
  localisation mechanism that we know of. Also write a PT-BR root README (`README.pt-BR.md`) and link both ways.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, then choose
  WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store. Copy the publish-extension
  skill to `.claude/skills/` when starting.
- **Raycast Store**: `author` is set to the Raycast username and `ray lint` passes; publishing itself is not started.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Flow refresh dialog**: `lx !refresh` / `lx !games` flashes the progress box and closes Flow (predates the
  monorepo). Check how `ShowProgressBoxAsync` interacts with the window hiding after the result action returns `false`.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
