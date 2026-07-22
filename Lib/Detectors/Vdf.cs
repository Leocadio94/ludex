using System.Text;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

/// <summary>
/// Minimal reader for Valve Data Format (.acf / .vdf). Keys are lowercased; values are
/// either string or Dictionary&lt;string, object&gt;.
/// </summary>
internal static class Vdf
{
    public static Dictionary<string, object> Parse(string content)
    {
        var tokens = Tokenize(content);
        var pos = 0;

        // Files start with a root key followed by a brace; .acf and libraryfolders.vdf both do.
        if (tokens.Count > 0 && tokens[0] != "{") pos++;
        if (pos < tokens.Count && tokens[pos] == "{") pos++;

        return ParseObject(tokens, ref pos);
    }

    private static List<string> Tokenize(string content)
    {
        var tokens = new List<string>();
        var i = 0;

        while (i < content.Length)
        {
            var ch = content[i];

            if (char.IsWhiteSpace(ch))
            {
                i++;
            }
            else if (ch == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                while (i < content.Length && content[i] != '\n') i++;
            }
            else if (ch == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (i < content.Length && content[i] != '"')
                {
                    if (content[i] == '\\' && i + 1 < content.Length)
                    {
                        i++;
                        sb.Append(content[i] switch { 'n' => '\n', 't' => '\t', var c => c });
                    }
                    else
                    {
                        sb.Append(content[i]);
                    }
                    i++;
                }
                i++;
                tokens.Add(sb.ToString());
            }
            else if (ch is '{' or '}')
            {
                tokens.Add(ch.ToString());
                i++;
            }
            else
            {
                var start = i;
                while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] is not ('{' or '}')) i++;
                if (i > start) tokens.Add(content[start..i]);
            }
        }

        return tokens;
    }

    private static Dictionary<string, object> ParseObject(List<string> tokens, ref int pos)
    {
        var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        while (pos < tokens.Count)
        {
            var token = tokens[pos];
            if (token == "}")
            {
                pos++;
                break;
            }

            var key = token;
            pos++;
            if (pos >= tokens.Count) break;

            if (tokens[pos] == "{")
            {
                pos++;
                obj[key] = ParseObject(tokens, ref pos);
            }
            else
            {
                obj[key] = tokens[pos];
                pos++;
            }
        }

        return obj;
    }
}
