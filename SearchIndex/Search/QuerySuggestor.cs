using System;

using Kennedy.SearchIndex.Models;

namespace Kennedy.SearchIndex.Search
{
	/// <summary>
	/// Suggest new queries
	/// </summary>
	public static class QuerySuggestor
	{
		public static UserQuery MakeOrQuery(UserQuery originalQuery)
		{
			string newTermQuery = MakeOrQuery(originalQuery.TermsQuery) ?? "";

			string newRawQuery = newTermQuery + " " + originalQuery.SiteScope;
            newRawQuery = newRawQuery.Trim();

            return new UserQuery
			{
				RawQuery = newRawQuery!,
				SiteScope = originalQuery.SiteScope,
				TermsQuery = newTermQuery,
				FTSQuery = FtsSyntaxConverter.Convert(newTermQuery)
            };
		}

		private static string? MakeOrQuery(string? terms)
		{
			if(terms != null)
			{
				return string.Join(" OR ", terms.Split(' '));
			}
			return null;
		}
	}
}

