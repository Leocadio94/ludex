# Pending

Open items that are not part of the current phase. Move each to an issue once someone owns it.

- **Raycast `<root>`**: `raycast/src/lib/path-resolver.ts` uses `dirname(installPath)`, so Steam games whose
  only rule is `<root>/userdata/...` (Onimusha: Way of the Sword) end up with no paths and are dropped.
  Fix: `<root>` = the folder containing `steamapps`.
- **README**: logo, badges, screenshots and a short usage GIF, once the Command Palette UI exists.
- **Third-party notices**: Ludusavi manifest (MIT), PCGamingWiki data (CC BY-NC-SA 3.0), Flow.Launcher.Plugin, Windows App SDK.
- **Command Palette publishing**: replace the template `Identity/@Publisher` in `Package.appxmanifest`, then choose
  WinGet (Inno Setup, see `docs/reference/cmdpal-template`) or the Microsoft Store.
- **Raycast Store**: `author` in `raycast/package.json` must be a Raycast username to publish.
- **Old repo**: archive `Leocadio94/raycast-game-saves-manager` once the monorepo copy is confirmed.
- **ARM64**: the solution now maps an ARM64 platform for the extension; nothing has been run on ARM64.
