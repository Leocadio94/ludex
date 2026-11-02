# Third-party notices

Ludex itself is [MIT](LICENSE). It relies on the following data and software, each under its own terms.

## Data

| Source | Use | Terms |
|---|---|---|
| [Ludusavi manifest](https://github.com/mtkennerly/ludusavi-manifest) by Matthew T. Kennerly | Where every game keeps its saves and config. Downloaded at runtime and cached locally; never bundled. | MIT |
| [PCGamingWiki](https://www.pcgamingwiki.com) | The manifest is compiled from its articles. | CC BY-NC-SA 3.0 |

If a game's paths are wrong or missing, fix them at the source (the PCGamingWiki article); the correction
reaches Ludex through the manifest on the next refresh.

## Software

| Component | Used by | Terms |
|---|---|---|
| [Flow.Launcher.Plugin](https://www.nuget.org/packages/Flow.Launcher.Plugin) | Flow Launcher plugin | MIT |
| [Microsoft.CommandPalette.Extensions](https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions) and its toolkit | Command Palette extension | MIT |
| [Microsoft.Windows.CsWinRT](https://www.nuget.org/packages/Microsoft.Windows.CsWinRT) | Command Palette extension | MIT |
| [Shmuelie.WinRTServer](https://www.nuget.org/packages/Shmuelie.WinRTServer) | Command Palette extension | MIT |
| [JetBrains.Annotations](https://www.nuget.org/packages/JetBrains.Annotations) | Flow Launcher plugin (via Flow.Launcher.Plugin) | MIT |
| [@raycast/api](https://www.npmjs.com/package/@raycast/api), [@raycast/utils](https://www.npmjs.com/package/@raycast/utils) | Raycast extension | MIT |

The Command Palette extension was generated from Microsoft's "Create a new extension" template. Files that
still carry the Microsoft copyright header (`Ludex.cs`, `Program.cs`, `Helpers/SettingsManager.cs` and
`LudexCommandsProvider.cs` under `src/CommandPalette`) are MIT-licensed by Microsoft and keep that header.

Build-time only, not redistributed: Microsoft.Windows.SDK.BuildTools.MSIX (Microsoft SDK licence).

The Flow Launcher, PowerToys and Raycast names belong to their owners. Ludex is not affiliated with them
or with Ludusavi.
