namespace Kennedy.SearchIndex.Search;

using System;
using System.Text;
using System.Text.RegularExpressions;

using Kennedy.SearchIndex.Models;

public class QueryParser
{

	readonly static Regex whitespaceRuns = new Regex(@"\s+");

	readonly static Regex siteScopeRegex = new Regex(@"\bsite\:\s*([a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    readonly static Regex fileTypeScopeRegex = new Regex(@"\bfiletype\:\s*([a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UserQuery Parse(string inputQuery)
	{
		string rawQuery = Normalize(inputQuery);
		string termsQuery = rawQuery;

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

		return new UserQuery
		{
			FileTypeScope = fileTypeScope,
			FTSQuery = FtsSyntaxConverter.Convert(termsQuery),
			RawQuery = inputQuery,
			SiteScope = siteScope,
			TermsQuery = termsQuery,
		};
	}

	private string Normalize(string s)
	{
		s = s.Trim();
		return whitespaceRuns.Replace(s, " ");
	}

	private string? GetSearchOption(string query, Regex rule)
	{
		var match = rule.Match(query);
		if(!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

	private string RemoveSearchOption(string query, Regex regex)
		=> Normalize(regex.Replace(query, ""));
}

