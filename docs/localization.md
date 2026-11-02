# Localisation

English is the default everywhere; Brazilian Portuguese (`pt-BR`) is the first translation.

| Front end | Status |
|---|---|
| Flow Launcher | Translated. Follows the language chosen in Flow. |
| Command Palette | Translated. Follows the Windows display language, read when the extension starts, so run `Reload` after changing it. |
| Raycast | English only by design: the Raycast Store supports US English only and asks extensions not to add their own localisation. |

## How it works (C#)

All user-visible text goes through `Texts.T` / `Texts.F` in `src/Core`. The English literal is the lookup
key; `Texts.PtBr.cs` maps it to Portuguese and a missing entry falls back to English. Core has no resource
files, so the same strings serve both C# front ends and stay trim-safe.

`Texts.Culture` starts as invariant (English). A front end opts in by setting it; Flow does so from
`CultureInfo.CurrentUICulture` at start-up and again from `IPluginI18n.OnCultureInfoChanged`; Command Palette
sets it from `CultureInfo.CurrentUICulture` in `LudexApp`.

Flow's documented route is per-culture `Languages/*.xaml` files read with `API.GetTranslation`. That was not used
because those files would not be reusable by Command Palette.

## Adding or changing text

1. Write the English text at the call site: `T("Launch Game")`, or `F("{0} saves", count)` for formats.
2. Add the same literal as a key in `Texts.PtBr.cs`.
3. Changing an English literal orphans its translation, so edit both together.

The typed commands (`!games`, `!refresh`, `!all`) are never translated; Flow matches them by their English name
as well as by the translated title.

## Adding a language

Add a dictionary beside `PtBr`, and pick it in `Texts.T` by `Culture.TwoLetterISOLanguageName`.
