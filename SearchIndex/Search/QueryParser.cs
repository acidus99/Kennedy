namespace Kennedy.SearchIndex.Search;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Kennedy.SearchIndex.Models;

public class QueryParser
{

	readonly static Regex whitespaceRuns = new Regex(@"\s+");

	readonly static Regex siteScopeRegex = new Regex(@"\bsite\:\s*([0-9a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    readonly static Regex fileTypeScopeRegex = new Regex(@"\bfiletype\:\s*([0-9a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	readonly static Regex[] titleScopeRegexes =
	{
		new Regex(@"\bintitle:\s*([^\""\s]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
		new Regex(@"\bintitle:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
	};

	readonly static Regex urlScopeRegex = new Regex(@"\binurl:\s*""?([^\s\""]+)""?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UserQuery Parse(string inputQuery)
	{
		string rawQuery = Normalize(inputQuery);
		string termsQuery = rawQuery;

		string? titleScope = GetSearchOption(termsQuery, titleScopeRegexes);
		if(titleScope != null)
		{
			termsQuery = RemoveSearchOption(termsQuery, titleScopeRegexes);
		}

		string? siteScope = GetSearchOption(termsQuery, siteScopeRegex);
		if(siteScope != null)
		{
			termsQuery = RemoveSearchOption(termsQuery, siteScopeRegex);
		}

		string? fileTypeScope = GetSearchOption(termsQuery, fileTypeScopeRegex);
		if(fileTypeScope != null)
		{
			termsQuery = RemoveSearchOption(termsQuery, fileTypeScopeRegex);
		}

		string? urlScope = GetSearchOption(termsQuery, urlScopeRegex);
		if(urlScope != null)
		{
			termsQuery = RemoveSearchOption(termsQuery, urlScopeRegex);
        }

		return new UserQuery
		{
			FileTypeScope = fileTypeScope,
			FTSQuery = FtsSyntaxConverter.Convert(termsQuery),
			RawQuery = inputQuery,
			SiteScope = siteScope,
			TermsQuery = termsQuery,
			TitleScope = titleScope,
			UrlScope = urlScope
		};
	}

	private string Normalize(string s)
	{
		s = s.Trim();
		return whitespaceRuns.Replace(s, " ");
	}

    private string? GetSearchOption(string query, IEnumerable<Regex> regexes)
	{
		foreach(var regex in regexes)
		{
			string? result = GetSearchOption(query, regex);
			if(result != null)
			{
				return result;
			}
		}
		return null;
	}

    private string? GetSearchOption(string query, Regex rule)
	{
		var match = rule.Match(query);
		if(!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value.ToLower();
	}

	private string RemoveSearchOption(string query, IEnumerable<Regex> regexes)
	{
        foreach (var regex in regexes)
        {
			query = RemoveSearchOption(query, regex);
        }
        return query;
    } 

	private string RemoveSearchOption(string query, Regex regex)
		=> Normalize(regex.Replace(query, ""));
}

