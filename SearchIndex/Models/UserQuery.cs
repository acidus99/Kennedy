namespace Kennedy.SearchIndex.Models;

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

    public bool HasUrlScope
        => !string.IsNullOrEmpty(UrlScope);

    //A simple query only has an FTS component, and no other scopes
    public bool IsSimpleQuery
        => HasFtsQuery && !(HasSiteScope || HasFileTypeScope || HasTitleScope || HasUrlScope);

    public string? UrlScope { get; set; }

    public string? TitleScope { get; set; }
    public required string RawQuery { get; set; }

    public string? SiteScope { get; set; }

    public string? TermsQuery { get; set; }

    /// <summary>
    /// Does this query have the need components for a text search?
    /// </summary>
    public bool IsValidTextQuery
        => HasFtsQuery || HasSiteScope || HasFileTypeScope || HasTitleScope || HasUrlScope;

    /// <summary>
    /// Does this query have the need components for an image search?
    /// </summary>
    public bool IsValidImageQuery
        => (HasFtsQuery || HasSiteScope || HasFileTypeScope || HasUrlScope) && !HasTitleScope;

    public override string ToString()
        => RawQuery;
}