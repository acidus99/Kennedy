using System.Text.RegularExpressions;
using Kennedy.Search.Models;

namespace Kennedy.Search.Query;

public sealed class QueryParser
{
    private static readonly Regex WhitespaceRuns = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SiteScopeRegex = new(@"\bsite\:\s*([0-9a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileTypeScopeRegex = new(@"\bfiletype\:\s*([0-9a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex[] TitleScopeRegexes =
    {
        new Regex(@"\bintitle:\s*([^\""\s]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bintitle:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };
    private static readonly Regex UrlScopeRegex = new(@"\binurl:\s*\""?([^\s\""]+)\""?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UserQuery Parse(string inputQuery)
    {
        string normalized = Normalize(inputQuery);
        string terms = normalized;

        string? titleScope = GetSearchOption(terms, TitleScopeRegexes);
        if (titleScope != null)
        {
            terms = RemoveSearchOption(terms, TitleScopeRegexes);
        }

        string? siteScope = GetSearchOption(terms, SiteScopeRegex);
        if (siteScope != null)
        {
            terms = RemoveSearchOption(terms, SiteScopeRegex);
        }

        string? fileTypeScope = GetSearchOption(terms, FileTypeScopeRegex);
        if (fileTypeScope != null)
        {
            terms = RemoveSearchOption(terms, FileTypeScopeRegex);
        }

        string? urlScope = GetSearchOption(terms, UrlScopeRegex);
        if (urlScope != null)
        {
            terms = RemoveSearchOption(terms, UrlScopeRegex);
        }

        return new UserQuery
        {
            RawQuery = inputQuery,
            TermsQuery = terms,
            FtsQuery = string.IsNullOrWhiteSpace(terms) ? null : FtsSyntaxConverter.Convert(terms),
            SiteScope = siteScope,
            FileTypeScope = fileTypeScope,
            TitleScope = titleScope,
            UrlScope = urlScope
        };
    }

    private static string Normalize(string s)
        => WhitespaceRuns.Replace(s.Trim(), " ");

    private static string? GetSearchOption(string query, IEnumerable<Regex> regexes)
    {
        foreach (var regex in regexes)
        {
            var result = GetSearchOption(query, regex);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private static string? GetSearchOption(string query, Regex rule)
    {
        var match = rule.Match(query);
        if (!match.Success)
        {
            return null;
        }
        return match.Groups[1].Value.ToLowerInvariant();
    }

    private static string RemoveSearchOption(string query, IEnumerable<Regex> regexes)
    {
        foreach (var regex in regexes)
        {
            query = RemoveSearchOption(query, regex);
        }
        return query;
    }

    private static string RemoveSearchOption(string query, Regex regex)
        => Normalize(regex.Replace(query, string.Empty));
}
