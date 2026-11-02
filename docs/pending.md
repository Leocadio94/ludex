# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **Raycast: Onimusha missing**: "Onimusha: Way of the Sword" shows in Flow and Command Palette but not in Raycast,
  even after the `<root>` fix and a refresh. Other games (Enshrouded, Ender Lilies) appear, so it is specific to
  this game's manifest entry or detection. Not investigated yet.
- **Raycast icon**: Raycast may still show the old Game Saves Manager icon next to the Ludex one; likely the old
  extension is still installed in Raycast. Check after uninstalling it.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, then choose
  WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store. Copy the publish-extension
  skill to `.claude/skills/` when starting.
- **Raycast Store**: `author` in `raycast/package.json` must be a Raycast username to publish.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Flow refresh dialog**: `lx !refresh` / `lx !games` flashes the progress box and closes Flow (predates the
  monorepo). Check how `ShowProgressBoxAsync` interacts with the window hiding after the result action returns `false`.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
