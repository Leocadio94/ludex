using System.Globalization;

namespace Ludex.Core;

/// <summary>
/// User-visible text. The English literal is the lookup key, so a missing translation shows English.
/// </summary>
/// <remarks>See docs/localization.md.</remarks>
public static partial class Texts
{
    /// <summary>Language of the translated text. Invariant means English; each front end opts in.</summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    public static string T(string english) =>
        Culture.TwoLetterISOLanguageName == "pt" && PtBr.TryGetValue(english, out var translated)
            ? translated
            : english;

    public static string F(string english, params object[] args) =>
        string.Format(Culture, T(english), args);
}
