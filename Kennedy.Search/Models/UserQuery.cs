namespace Kennedy.Search.Models;

/// <summary>
/// Normalized user query + optional search scopes.
/// </summary>
public sealed class UserQuery
{
    public required string RawQuery { get; init; }
    public string TermsQuery { get; init; } = string.Empty;
    public string? FtsQuery { get; init; }
    public string? SiteScope { get; init; }
    public string? FileTypeScope { get; init; }
    public string? TitleScope { get; init; }
    public string? UrlScope { get; init; }

    public bool HasFtsQuery => !string.IsNullOrEmpty(FtsQuery);
    public bool HasSiteScope => !string.IsNullOrEmpty(SiteScope);
    public bool HasFileTypeScope => !string.IsNullOrEmpty(FileTypeScope);
    public bool HasTitleScope => !string.IsNullOrEmpty(TitleScope);
    public bool HasUrlScope => !string.IsNullOrEmpty(UrlScope);

    public bool IsValidTextQuery => HasFtsQuery || HasSiteScope || HasFileTypeScope || HasTitleScope || HasUrlScope;
    public bool IsValidImageQuery => (HasFtsQuery || HasSiteScope || HasFileTypeScope || HasUrlScope) && !HasTitleScope;
    public bool IsSimpleQuery => HasFtsQuery && !(HasSiteScope || HasFileTypeScope || HasTitleScope || HasUrlScope);

    public override string ToString()
        => RawQuery;
}
