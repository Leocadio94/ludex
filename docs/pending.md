# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **Translations (PT-BR) in Command Palette**: Flow and the Core text are translated and `README.pt-BR.md` exists (see `docs/localization.md`). Command Palette still needs its own pages (`Pages/`, `Commands/`, `Helpers/`, settings) moved to `Texts.T`, and `Texts.Culture` set from the system UI culture. Its docs say nothing about localisation, so check that the culture reaches the extension process. Raycast stays English.
- **Flow Launcher publishing**: `.github/workflows/release-flow.yml` builds a zip on a `flow-v*` tag but has never run. Needs a public repo, a tag matching `plugin.json` Version, then a submission to the Flow plugin store (a pull request to the Flow Launcher plugin manifest repository; check its current requirements first). The plugin ID must not change.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, then choose
  WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store. Copy the publish-extension
  skill to `.claude/skills/` when starting.
- **Raycast Store**: `author` is set to the Raycast username and `ray lint` passes; publishing itself is not started.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Flow refresh dialog**: `lx !refresh` / `lx !games` flashes the progress box and closes Flow (predates the
  monorepo). Check how `ShowProgressBoxAsync` interacts with the window hiding after the result action returns `false`.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
