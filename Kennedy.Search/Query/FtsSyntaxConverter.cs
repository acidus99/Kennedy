using System.Text;

namespace Kennedy.Search.Query;

/// <summary>
/// Converts a plain-language user query into valid SQLite FTS5 query syntax.
/// Key transformations:
/// <list type="bullet">
///   <item>Bare words are wrapped in implicit double-quotes so FTS5 treats them as exact token matches.</item>
///   <item>The boolean operators AND, OR, NOT (all-caps) are passed through as FTS5 operators.</item>
///   <item>User-supplied double-quoted phrases are preserved as FTS5 phrase queries.</item>
///   <item>Single quotes are escaped as <c>''</c> to prevent FTS5 syntax errors.</item>
/// </list>
/// </summary>
public static class FtsSyntaxConverter
{
    /// <summary>
    /// Converts a user-oriented query into SQLite FTS5 syntax while preserving quoted sections.
    /// The converter is a character-by-character state machine to handle quote nesting and operator detection.
    /// </summary>
    public static string Convert(string inputQuery)
    {
        bool inQuote = false;
        bool implicitQuote = false;

        const char None = '\x00';
        // Sentinel: we saw the start of a potential keyword (AND/OR/NOT), waiting for whitespace/paren to confirm.
        const char WordSeparator = '\x01';
        const string WordEnders = " \t\n(\"";

        StringBuilder output = new();
        StringBuilder pending = new();
        char expect = None;

        foreach (char c in inputQuery)
        {
            if (c == '\'')
            {
                if (pending.Length != 0)
                {
                    if (!implicitQuote)
                    {
                        output.Append('"');
                        implicitQuote = true;
                    }
                    output.Append(pending);
                    pending.Clear();
                }
                output.Append("''");
                continue;
            }

            if (expect != None)
            {
                if (inQuote)
                {
                    throw new ApplicationException("FTS conversion parser error");
                }

                if ((expect == WordSeparator && !WordEnders.Contains(c)) || (expect != WordSeparator && c != expect))
                {
                    output.Append('"');
                    output.Append(pending);
                    implicitQuote = true;
                    expect = None;
                    pending.Clear();
                }
                else if (expect == WordSeparator)
                {
                    output.Append(pending);
                    expect = None;
                    pending.Clear();
                }
                else
                {
                    pending.Append(c);
                    expect = expect switch
                    {
                        'N' => 'D',
                        'O' => 'T',
                        'D' or 'R' or 'T' => WordSeparator,
                        _ => throw new ApplicationException("FTS conversion parser error")
                    };
                    continue;
                }
            }

            if (c == '"')
            {
                if (implicitQuote)
                {
                    implicitQuote = false;
                    inQuote = true;
                }
                else
                {
                    inQuote = !inQuote;
                    output.Append(c);
                }
            }
            else if (!inQuote && !implicitQuote && "AON".Contains(c))
            {
                expect = c switch
                {
                    'A' => 'N',
                    'O' => 'R',
                    'N' => 'O',
                    _ => throw new ApplicationException("FTS conversion parser error")
                };
                pending.Clear();
                pending.Append(c);
            }
            else if (" \t\n()".Contains(c))
            {
                if (implicitQuote)
                {
                    output.Append('"');
                    implicitQuote = false;
                    inQuote = false;
                }
                output.Append(c);
            }
            else
            {
                if (inQuote || implicitQuote)
                {
                    output.Append(c);
                }
                else
                {
                    inQuote = true;
                    implicitQuote = true;
                    output.Append('"');
                    output.Append(c);
                }
            }
        }

        output.Append(pending);
        if (inQuote || implicitQuote)
        {
            output.Append('"');
        }

        return output.ToString().Trim();
    }
}
