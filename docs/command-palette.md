# Command Palette extension: build, deploy, debug

## Prerequisites

- Windows 11 with PowerToys and Command Palette enabled.
- Developer Mode on (Settings > System > For developers).
- Visual Studio with the workloads in [`.vsconfig`](../.vsconfig) (WinUI application development, Windows 11 SDK 26100).

## Deploy

1. Open `Ludex.sln`, choose **Debug** and **x64**.
2. **Build > Deploy Ludex**. Building alone does not update the installed package.
3. In Command Palette, run `Reload` and pick the one whose subtitle is "Reload Command Palette Extension".

If "Deploy" is missing from the menu, the solution lost its `Deploy.0` entries for the extension project
(Configuration Manager > tick *Deploy*). `dotnet sln add` never writes them.

The extension is a packaged app, so its settings and cache live under
`%LOCALAPPDATA%\Packages\Ludex_8wekyb3d8bbwe\LocalState`, not next to the Flow plugin's cache.

## Alias

Command Palette gives extensions no way to ship a default alias. To type `lx`, open Settings > Extensions >
Ludex and set the alias on the "Ludex" command.

## Gotchas

- Status messages and toasts raised through `ExtensionHost` are not shown when a command runs from the home
  page. Refresh progress therefore lives in `RefreshPage`; one-off messages use `CommandResult.ShowToast`.
- The toolkit's fuzzy matcher is internal, so "Search all games" uses `Helpers/Fuzzy.cs`.
- Command Palette has no usage ranking for items inside an extension, so "most used first" is `UsageTracker`.
- Release publishes trimmed. Keep anything added to Core trim-safe: source-generated JSON only.
- The CLSID in `Ludex.cs` (`[Guid]`) must match `Package.appxmanifest` in both places that name it.

## Publishing

Not done yet. The template's guides for WinGet (Inno Setup + GitHub Actions) and the Microsoft Store are kept in
[`reference/cmdpal-template`](reference/cmdpal-template). Before publishing, replace the template
`Identity/@Publisher` in `Package.appxmanifest`.
