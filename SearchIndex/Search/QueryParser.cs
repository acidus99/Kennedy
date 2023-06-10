namespace Kennedy.SearchIndex.Search;

using System;
using System.Text;
using System.Text.RegularExpressions;

using Kennedy.SearchIndex.Models;

public class QueryParser
{

	readonly static Regex whitespaceRuns = new Regex(@"\s+");

	readonly static Regex siteScope = new Regex(@"\bsite\:\s*([a-z\-\.]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UserQuery Parse(string inputQuery)
	{
		inputQuery = Normalize(inputQuery);

		string termsQuery = CreateTermQuery(inputQuery);

		return new UserQuery
		{
			RawQuery = inputQuery,
			TermsQuery = termsQuery,
			FTSQuery = FtsSyntaxConverter.Convert(termsQuery),
			SiteScope = ParseSiteScopeRules(termsQuery, inputQuery)
		};
	}

	private string Normalize(string s)
	{
		s = s.Trim();
		return whitespaceRuns.Replace(s, " ");
	}

	private string CreateTermQuery(string inputQuery)
	{
		return Normalize(siteScope.Replace(inputQuery, ""));
	}

	private string? ParseSiteScopeRules(string termsQuery, string inputQuery)
	{
		//optimization, there are only site scoping rules if we trimmed them out earlier
		//no need to run the regex twice if they are the same
		if(termsQuery == inputQuery)
		{
			return null;
		}

		var match = siteScope.Match(inputQuery);
		if(!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

		
}

