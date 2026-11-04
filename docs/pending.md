# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **README install section**: when a store publishes Ludex, rewrite "Install" in `README.md` and `README.pt-BR.md`
  with that store's steps and keep "Build from source" for contributors.
- **Raycast Store**: submitted as [raycast/extensions#33081](https://github.com/raycast/extensions/pull/33081),
  marked ready for review. Answer review comments; each fix is another `npm run publish`, which updates the same
  pull request. Once merged, rewrite the Raycast row of "Install" in the READMEs.
- **Raycast details pane (after publish)**: show each game's paths in a `List.Item.Detail`, with the home folder as
  `%USERPROFILE%` so screenshots do not leak the Windows user name.
- **Command Palette publishing**: only the Microsoft Store works today. Command Palette finds extensions through 
  `AppExtensionCatalog`, so it needs package identity; an Inno Setup EXE that only writes the CLSID is never 
  discovered (tested on PowerToys 0.102.3001: nothing shows up; [PowerToys#38273]
  (https://github.com/microsoft/PowerToys/issues/38273) and [#44442](https://github.com/microsoft/PowerToys/issues/44442)
  are open). The WinGet guide in `docs/reference/cmdpal-template` is wrong on this, and on `InprocServer32`: real 
  installers use `LocalServer32` with `-RegisterProcessAsComServer`. Plan: open a Partner Center account, replace the 
  template `Identity/@Publisher` in `Package.appxmanifest` and `AppxPackage*` in `Ludex.csproj` with the Partner 
  Center values, build the x64 MSIX (add ARM64 only if it can be tested), submit, then add the gallery entry with an 
  `msstore` install source (pull request to `microsoft/CmdPal-Extensions`: `extension.json` and an icon; a Microsoft 
  CLA is required). Revisit WinGet when Command Palette supports unpackaged extensions.
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
