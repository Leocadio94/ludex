using System;

namespace Ludex;

/// <summary>
/// Subsequence matcher for "Search all games". The toolkit's own fuzzy matcher is internal, and
/// the manifest has ~14k names to scan per keystroke, so this is a plain single pass.
/// </summary>
internal static class Fuzzy
{
    /// <returns>0 when the query does not match; higher is better.</returns>
    public static int Score(string query, string target)
    {
        var score = 0;
        var streak = 0;
        var matched = 0;
        var needed = 0;

        foreach (var c in query)
            if (!char.IsWhiteSpace(c)) needed++;

        if (needed == 0) return 0;

        var qi = 0;
        for (var ti = 0; ti < target.Length && matched < needed; ti++)
        {
            while (qi < query.Length && char.IsWhiteSpace(query[qi])) qi++;

            if (char.ToUpperInvariant(target[ti]) != char.ToUpperInvariant(query[qi]))
            {
                streak = 0;
                continue;
            }

            score += 1 + (streak * 2);
            if (ti == 0 || !char.IsLetterOrDigit(target[ti - 1])) score += 5;

            streak++;
            qi++;
            matched++;
        }

        if (matched < needed) return 0;

        if (target.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 20;
        else if (target.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 10;

        return Math.Max(1, score - ((target.Length - needed) / 4));
    }
}
