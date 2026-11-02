# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **README install section**: when a store publishes Ludex, rewrite "Install" in `README.md` and `README.pt-BR.md`
  with that store's steps and keep "Build from source" for contributors.
- **Raycast Store**: `author` is set and `ray lint` passes. Before `npm run publish`:
  - add 3 to 6 screenshots (2000x1250 PNG, via Capture Window in dev mode, "Save to Metadata");
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
