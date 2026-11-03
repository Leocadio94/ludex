# Publishing the Flow Launcher plugin

How a release reaches the Flow plugin store, and the constraints of the store's updater that the repository has to
respect. Source: [Flow.Launcher.PluginsManifest](https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest)
(its README and `ci/src`).

## Release

1. Bump `Version` in `src/FlowLauncher/plugin.json` and in `Flow.Launcher.Plugin.Ludex.csproj`, and move the
   Unreleased entries of `src/FlowLauncher/CHANGELOG.md` under the new version.
2. Tag the commit `vX.Y.Z` and push the tag. `.github/workflows/release-flow.yml` builds the plugin, fails if the
   tag differs from `plugin.json` Version, and attaches `Flow.Launcher.Plugin.Ludex-X.Y.Z.zip` to a GitHub release.

## Constraints from the store updater

- **The tag cannot contain a hyphen.** The manifest validator requires `UrlDownload` to match
  `releases/download/<letters, digits, dots>/<file>.zip`, so `flow-v1.0.0` is rejected and `v1.0.0` is accepted.
- **The updater reads `releases/latest` of the repository.** In this monorepo that must always be the Flow release.
  Releases of the other launchers (for example the Command Palette) must be published with `make_latest: false`,
  or as pre-releases, or the Flow plugin would be updated from the wrong assets.
- **The release holds one zip** with `plugin.json` at its root. The updater refreshes `Description`, `Author`,
  `Language`, `Website` and `MinimumAppVersion` from that file.
- `MinimumAppVersion` is `2.0.0` because the plugin targets .NET 9, which Flow adopted in 2.0.0. It has only been
  tested on Flow 2.1.3 and 2.1.4.

## First submission

One pull request to `Flow-Launcher/Flow.Launcher.PluginsManifest`, adding
`plugins/Ludex-6F3B2A9C-1D74-4E58-9C21-8A5E7B4D0F63.json` (name, a dash, the plugin ID):

```json
{
  "ID": "6F3B2A9C-1D74-4E58-9C21-8A5E7B4D0F63",
  "Name": "Ludex",
  "Description": "Launch installed games and open their save, config or install folders",
  "Author": "Gabriel Leocádio",
  "Version": "1.0.0",
  "Language": "csharp",
  "Website": "https://github.com/Leocadio94/ludex",
  "UrlDownload": "https://github.com/Leocadio94/ludex/releases/download/v1.0.0/Flow.Launcher.Plugin.Ludex-1.0.0.zip",
  "UrlSourceCode": "https://github.com/Leocadio94/ludex/tree/main/src/FlowLauncher",
  "IcoPath": "https://cdn.jsdelivr.net/gh/Leocadio94/ludex@v1.0.0/src/FlowLauncher/Images/icon.png",
  "MinimumAppVersion": "2.0.0"
}
```

The plugin ID must never change. After approval the store's CI checks for new releases every three hours, so later
versions need only a new tag. Approval is by the Flow team, submissions are scanned with VirusTotal, and the CDN can
take up to a week to propagate. Until then `pm install <zip URL>` works in Flow.

The store has no download statistics. GitHub shows download counts per release asset.
