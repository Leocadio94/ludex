# CLAUDE.md

Ludex: lists installed PC games and opens their save, config and install folders (and launches them) from
Flow Launcher, PowerToys Command Palette and Raycast. Long-form docs live in [`docs/`](docs/README.md);
this file keeps only what breaks silently.

## Commands

```powershell
dotnet build Ludex.sln -c Release   # Core + Flow plugin + Command Palette extension
.\scripts\deploy.ps1                # publish + install the Flow plugin, restart Flow (-NoRestart to skip)
.\scripts\make-icons.ps1            # Flow's Images/*.png from Segoe Fluent Icons glyphs
cd raycast; npm run build           # Raycast extension (own toolchain)
```

Flow holds the plugin DLL open, so `deploy.ps1` stops it first. Flow logs: `%APPDATA%\FlowLauncher\Logs\`.
The Command Palette extension must be **deployed from Visual Studio** (Debug | x64), then `Reload`:
see [docs/command-palette.md](docs/command-palette.md).

## Layout

`src/Core` (`Ludex.Core`, shared) · `src/FlowLauncher` · `src/CommandPalette` · `raycast` (TypeScript, independent).
How it fits together: [docs/architecture.md](docs/architecture.md), [docs/decisions](docs/decisions).

## Invariants

- **Core stays launcher-free and trim-safe.** The Command Palette extension publishes trimmed: source-generated
  JSON only, and `JsonStringEnumConverter<T>` rather than the non-generic converter.
- **`installed-games-v3.json`**: bump the suffix whenever `InstalledGame` gains a field, or old caches serve nulls.
- **`<root>` is the store root** (the folder containing `steamapps`), not the install directory's parent.
  `InstalledGame.StoreRoot` exists for this.
- **Wildcard fallback applies only to the final path segment.** A wildcard in the middle gets none, or a shared
  root like Steam's `userdata` would appear for every game.
- **Xbox detection reads the registry**, never `Get-AppxPackage` (tens of seconds).
- **Index property names are single letters** (~14.5k entries); the manifest is streamed to disk entry by entry,
  never materialized.
- **Flow Alphabetical sort** needs `AddSelectedCount = false`, or Flow re-sorts equal scores by usage.
- **Flow refresh**: `RunRefreshAsync` is called straight from the result action so the dialog is created on the
  UI thread; its task is deliberately not awaited.
- **Command Palette**: the `[Guid]` in `Ludex.cs` must match the CLSID in `Package.appxmanifest` (twice);
  `Deploy.0` entries in `Ludex.sln` are required for "Deploy" to exist; its `Directory.Build.props` /
  `Directory.Packages.props` stay inside `src/CommandPalette` so x64/ARM64 and central package versions don't leak.
- **Command Palette feedback**: `ExtensionHost` status messages and toasts are invisible from the home page; use
  `RefreshPage` for progress and `CommandResult.ShowToast` for one-off messages.
- **Flow plugin ID** (`6F3B2A9C-…` in `plugin.json`) must not change.
- The Raycast version deliberately differs from the C# pipeline; see the table in `docs/architecture.md` before
  "fixing" either side.
