# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **Translations (PT-BR)**: Raycast: not possible, its Store guidelines say it only supports US English and ask extensions not to add their own localisation. Flow Launcher: supported, via `IPluginI18n` (title, description, culture change) and `API.GetTranslation(key)` with per-culture `Languages/*.xaml` resource dictionaries, English as fallback. Command Palette: its docs say nothing about localisation; a packaged app may use Windows resource files (`Strings/<lang>/Resources.resw` and `ResourceLoader`), unverified and needs a trial. Also write `README.pt-BR.md` and link both ways. English stays the default everywhere.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, then choose
  WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store. Copy the publish-extension
  skill to `.claude/skills/` when starting.
- **Raycast Store**: `author` is set to the Raycast username and `ray lint` passes; publishing itself is not started.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Flow refresh dialog**: `lx !refresh` / `lx !games` flashes the progress box and closes Flow (predates the
  monorepo). Check how `ShowProgressBoxAsync` interacts with the window hiding after the result action returns `false`.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
