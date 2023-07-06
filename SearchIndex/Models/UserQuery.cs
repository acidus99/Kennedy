namespace Kennedy.SearchIndex.Models;

using System;

public class UserQuery
{
    public string? FileTypeScope { get; set; }

    public string? FTSQuery { get; set; }

    public bool HasFtsQuery
        => !string.IsNullOrEmpty(FTSQuery);

    public bool HasSiteScope
        => !string.IsNullOrEmpty(SiteScope);

    public bool HasFileTypeScope
        => !string.IsNullOrEmpty(FileTypeScope);

    public bool HasTitleScope
        => !string.IsNullOrEmpty(TitleScope);

    //A simple query only has an FTS component, and no other scopes
    public bool IsSimpleQuery
        => HasFtsQuery && !(HasSiteScope || HasFileTypeScope || HasTitleScope);

    public string? TitleScope { get; set; }
    public required string RawQuery { get; set; }

    public string? SiteScope { get; set; }

    public string? TermsQuery { get; set; }

    /// <summary>
    /// Does this query have the need components for a text search?
    /// </summary>
    public bool IsValidTextQuery
        => HasFtsQuery || HasSiteScope || HasFileTypeScope || HasTitleScope;

    /// <summary>
    /// Does this query have the need components for an image search?
    /// </summary>
    public bool IsValidImageQuery
        => (HasFtsQuery || HasSiteScope || HasFileTypeScope) && !HasTitleScope;

    public override string ToString()
        => RawQuery;
}

