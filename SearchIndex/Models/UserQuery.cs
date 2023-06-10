namespace Kennedy.SearchIndex.Models;

using System;

public class UserQuery
{
	public required string RawQuery { get; set; }

	public string? TermsQuery { get; set; }

	public string? FTSQuery { get; set; }

	public string? SiteScope { get; set; }

    public override string ToString()
        => RawQuery;
}

