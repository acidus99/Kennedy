using System;
using System.Text;

namespace Kennedy.SearchIndex.Search;

public static class FtsSyntaxConverter
{
    /// <summary>
    /// Takes a "Google style" search engine query and converts it into
    /// a SQLite FTS query
    /// </summary>
    /// <param name="inputQuery"></param>
    /// <returns></returns>
    /// <exception cref="ApplicationException"></exception>
    public static string Convert(string inputQuery)
    {
        bool inQuote = false;
        bool implicitQuote = false;

        const char None = '\x00';
        const char WordSeperator = '\x01';
        const string WordEnders = " \t\n(\"";


        StringBuilder output = new StringBuilder();
        StringBuilder pending = new StringBuilder();

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
                    output.Append(pending.ToString());
                    pending.Clear();
                }
                //double the single quote to escape it
                output.Append("''");
                continue;
            }
            if (expect != None)
            {
                if (inQuote)
                {
                    throw new ApplicationException("parser error");
                }
                if (
                    (expect == WordSeperator && !WordEnders.Contains(c)) ||
                    (expect != WordSeperator && c != expect)
                )
                {
                    output.Append('\"');
                    output.Append(pending.ToString());
                    implicitQuote = true;
                    expect = None;
                    pending.Clear();
                }
                else if (expect == WordSeperator)
                {
                    output.Append(pending.ToString());
                    expect = None;
                    pending.Clear();
                }
                else
                {
                    pending.Append(c);
                    switch (expect)
                    {
                        case 'N':
                            expect = 'D';
                            break;
                        case 'O':
                            expect = 'T';
                            break;

                        case 'D':
                        case 'R':
                        case 'T':
                            expect = WordSeperator;
                            break;

                        default:
                            throw new ApplicationException("parser error missing char");
                    }
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
                switch (c)
                {
                    case 'A':
                        expect = 'N';
                        break;
                    case 'O':
                        expect = 'R';
                        break;
                    case 'N':
                        expect = 'O';
                        break;
                    default:
                        throw new ApplicationException("parser error 4");
                }
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
        //out of the loop, clean up any pending characters and hanging quotes
        output.Append(pending);
        if (inQuote || implicitQuote)
        {
            output.Append('"');
        }

        return output.ToString().Trim();
    }
}