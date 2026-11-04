# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **README install section**: when a store publishes Ludex, rewrite "Install" in `README.md` and `README.pt-BR.md`
  with that store's steps and keep "Build from source" for contributors.
- **Raycast Store**: submitted as [raycast/extensions#33081](https://github.com/raycast/extensions/pull/33081),
  marked ready for review. Answer review comments; each fix is another `npm run publish`, which updates the same
  pull request. Once merged, rewrite the Raycast row of "Install" in the READMEs.
- **Raycast details pane (after publish)**: show each game's paths in a `List.Item.Detail`, with the home folder as
  `%USERPROFILE%` so screenshots do not leak the Windows user name.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, publish
  through WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store, then add the extension to
  the community gallery with a pull request to `microsoft/CmdPal-Extensions` (an `extension.json` and an icon pointing
  to the install source; a Microsoft CLA is required). Copy the publish-extension skill to `.claude/skills/` when
  starting.
- **Flow Launcher publishing**: ready to release. Push the tag `v1.0.0`, check the release zip, then open the
  manifest pull request. Steps and constraints: [flow-publishing.md](flow-publishing.md). Afterwards, rewrite
  "Install" in the READMEs.
- **Raycast publish automation (idea)**: release the Raycast extension from a workflow on a tag. `npm run publish`
  authenticates with a GitHub device code in a terminal; check whether the CLI accepts a token, and whether a
  pull request to `raycast/extensions` can be updated from CI.
- **Command Palette manifest text**: `DisplayName` and `Description` in `Package.appxmanifest` are still English;
  translating them needs Windows resource files.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
