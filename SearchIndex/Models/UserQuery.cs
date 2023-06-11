namespace Kennedy.SearchIndex.Models;

using System;

public class UserQuery
{
    public string? FTSQuery { get; set; }

    public bool HasFtsQuery
        => !string.IsNullOrEmpty(FTSQuery);

    public bool HasSiteScope
        => !string.IsNullOrEmpty(SiteScope);

    public required string RawQuery { get; set; }

    public string? SiteScope { get; set; }

    public string? TermsQuery { get; set; }

    public override string ToString()
        => RawQuery;
}

