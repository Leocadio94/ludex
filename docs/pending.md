# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **README install section**: when a store publishes Ludex, rewrite "Install" in `README.md` and `README.pt-BR.md`
  with that store's steps and keep "Build from source" for contributors.
- **Raycast Store**: the extension is ready (`ray lint` passes, 4 screenshots in `raycast/metadata`). Only
  `npm run publish` is left; it opens a pull request on the public `raycast/extensions` repo.
- **Raycast details pane (after publish)**: show each game's paths in a `List.Item.Detail`, with the home folder as
  `%USERPROFILE%` so screenshots do not leak the Windows user name.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, publish
  through WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store, then add the extension to
  the community gallery with a pull request to `microsoft/CmdPal-Extensions` (an `extension.json` and an icon pointing
  to the install source; a Microsoft CLA is required). Copy the publish-extension skill to `.claude/skills/` when
  starting.
- **Flow Launcher publishing**: `.github/workflows/release-flow.yml` builds a zip on a `flow-v*` tag but has never
  run. Needs a public repo and a tag matching `plugin.json` Version. Then a pull request to
  `Flow-Launcher/Flow.Launcher.PluginsManifest` adding a `plugins/<name>-<uuid>.json` with the `plugin.json` fields,
  `UrlDownload`, `UrlSourceCode` and an `IcoPath` served from a CDN such as jsDelivr. Submissions are scanned with
  VirusTotal, and listing can take days. The plugin ID must not change.
- **Command Palette manifest text**: `DisplayName` and `Description` in `Package.appxmanifest` are still English;
  translating them needs Windows resource files.
- **ARM64**: the solution maps an ARM64 platform for the extension; nothing has been run on ARM64.
- **Fallback command (idea)**: typing a game name on the Command Palette home page could offer "Search Ludex for
  ...". See `docs/reference/cmdpal-template/skills/add-fallback-commands`.
