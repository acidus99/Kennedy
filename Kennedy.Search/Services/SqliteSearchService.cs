using Kennedy.Search.Models;
using Microsoft.Data.Sqlite;

namespace Kennedy.Search.Services;

/// <summary>
/// Read-only search service for the search SQLite database.
/// This remains isolated from archive storage.
/// </summary>
public sealed class SqliteSearchService : ISearchService
{
    private readonly string _connectionString;

    public SqliteSearchService(string sqlitePath)
    {
        _connectionString = $"Data Source={sqlitePath}";
    }

    public int GetTextResultsCount(UserQuery query)
    {
        if (!query.IsValidTextQuery)
        {
            throw new ArgumentException("Not a valid text query.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        var textJoin = query.HasFtsQuery
            ? "INNER JOIN DocumentsFts ON DocumentsFts.rowid = d.Id"
            : "LEFT JOIN DocumentsFts ON DocumentsFts.rowid = d.Id";
        cmd.CommandText =
            $"""
            SELECT COUNT(*)
            FROM Documents d
            {textJoin}
            WHERE d.IsSearchable = 1
            """ + BuildTextFilters(query, cmd);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public IReadOnlyList<TextSearchResult> SearchText(UserQuery query, int offset, int limit)
    {
        if (!query.IsValidTextQuery)
        {
            throw new ArgumentException("Not a valid text query.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        var textJoin = query.HasFtsQuery
            ? "INNER JOIN DocumentsFts ON DocumentsFts.rowid = d.Id"
            : "LEFT JOIN DocumentsFts ON DocumentsFts.rowid = d.Id";
        cmd.CommandText =
            $"""
            SELECT d.CanonicalUrl,
                   d.Title,
                   d.MimeType,
                   d.DetectedLanguage,
                   d.LineCount,
                   d.BodySize,
                   d.IsBodyTruncated,
                   CASE
                       WHEN @fts_has = 1 THEN snippet(DocumentsFts, 1, '[',']','…',20)
                       ELSE substr(d.Content, 1, 180)
                   END AS Snippet
            FROM Documents d
            {textJoin}
            WHERE d.IsSearchable = 1
            """ + BuildTextFilters(query, cmd) + " ORDER BY d.LastIndexedUtc DESC LIMIT @limit OFFSET @offset;";

        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var results = new List<TextSearchResult>();
        SqliteDataReader reader;
        try
        {
            reader = cmd.ExecuteReader();
        }
        catch (SqliteException ex)
        {
            throw new SqliteException($"{ex.Message}\nSQL:\n{cmd.CommandText}", ex.SqliteErrorCode, ex.SqliteExtendedErrorCode);
        }
        using (reader)
        {
            while (reader.Read())
            {
                results.Add(new TextSearchResult
                {
                    Url = reader.GetString(0),
                    Title = reader.IsDBNull(1) ? null : reader.GetString(1),
                    MimeType = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DetectedLanguage = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LineCount = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    BodySize = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    IsBodyTruncated = !reader.IsDBNull(6) && reader.GetBoolean(6),
                    Snippet = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                });
            }
        }

        return results;
    }

    public int GetImageResultsCount(UserQuery query)
    {
        if (!query.IsValidImageQuery)
        {
            throw new ArgumentException("Not a valid image query.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        var filesJoin = query.HasFtsQuery
            ? "INNER JOIN FilesFts ON FilesFts.rowid = u.Id"
            : "LEFT JOIN FilesFts ON FilesFts.rowid = u.Id";
        cmd.CommandText =
            $"""
            SELECT COUNT(*)
            FROM UrlRegistry u
            {filesJoin}
            WHERE 1=1
            """ + BuildImageFilters(query, cmd);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public IReadOnlyList<ImageSearchResult> SearchImages(UserQuery query, int offset, int limit)
    {
        if (!query.IsValidImageQuery)
        {
            throw new ArgumentException("Not a valid image query.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        var filesJoin = query.HasFtsQuery
            ? "INNER JOIN FilesFts ON FilesFts.rowid = u.Id"
            : "LEFT JOIN FilesFts ON FilesFts.rowid = u.Id";
        cmd.CommandText =
            $"""
            SELECT u.NormalizedUrl,
                   u.ImageType,
                   COALESCE(u.ImageWidth, 0),
                   COALESCE(u.ImageHeight, 0),
                   0,
                   0,
                   CASE
                       WHEN @fts_has = 1 THEN snippet(FilesFts, 1, '[',']','…',20)
                       ELSE COALESCE(substr(FilesFts.SearchText, 1, 180), '')
                   END AS Snippet
            FROM UrlRegistry u
            {filesJoin}
            WHERE 1=1
            """ + BuildImageFilters(query, cmd) + " ORDER BY u.LastVisit DESC LIMIT @limit OFFSET @offset;";

        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var results = new List<ImageSearchResult>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ImageSearchResult
            {
                Url = reader.GetString(0),
                ImageType = reader.IsDBNull(1) ? null : reader.GetString(1),
                Width = reader.GetInt32(2),
                Height = reader.GetInt32(3),
                BodySize = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsBodyTruncated = !reader.IsDBNull(5) && reader.GetBoolean(5),
                Snippet = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }

        return results;
    }

    private static string BuildTextFilters(UserQuery query, SqliteCommand cmd)
    {
        cmd.Parameters.AddWithValue("@fts_has", query.HasFtsQuery ? 1 : 0);

        var filters = new List<string>();

        if (query.HasFtsQuery)
        {
            filters.Add("DocumentsFts MATCH @fts_query");
            cmd.Parameters.AddWithValue("@fts_query", query.FtsQuery!);
        }

        if (query.HasTitleScope)
        {
            filters.Add("COALESCE(d.Title, '') LIKE @title_scope");
            cmd.Parameters.AddWithValue("@title_scope", $"%{query.TitleScope}%");
        }

        AppendCommonDocumentFilters(query, cmd, filters);

        return filters.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", filters);
    }

    private static string BuildImageFilters(UserQuery query, SqliteCommand cmd)
    {
        cmd.Parameters.AddWithValue("@fts_has", query.HasFtsQuery ? 1 : 0);
        var filters = new List<string>();
        filters.Add("u.IsImage = 1");

        if (query.HasFtsQuery)
        {
            filters.Add("FilesFts MATCH @fts_query");
            cmd.Parameters.AddWithValue("@fts_query", query.FtsQuery!);
        }

        AppendCommonUrlFilters(query, cmd, filters);

        return filters.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", filters);
    }

    private static void AppendCommonDocumentFilters(UserQuery query, SqliteCommand cmd, List<string> filters)
    {
        if (query.HasSiteScope)
        {
            filters.Add("d.CanonicalUrl LIKE @site_scope");
            cmd.Parameters.AddWithValue("@site_scope", $"gemini://{query.SiteScope}/%");
        }

        if (query.HasFileTypeScope)
        {
            // Map filetype scope to mime fragments in current schema.
            filters.Add("COALESCE(d.MimeType, '') LIKE @filetype_scope");
            cmd.Parameters.AddWithValue("@filetype_scope", $"%{query.FileTypeScope}%");
        }

        if (query.HasUrlScope)
        {
            filters.Add("d.CanonicalUrl LIKE @url_scope");
            cmd.Parameters.AddWithValue("@url_scope", $"%{query.UrlScope}%");
        }
    }

    private static void AppendCommonUrlFilters(UserQuery query, SqliteCommand cmd, List<string> filters)
    {
        if (query.HasSiteScope)
        {
            filters.Add("u.NormalizedUrl LIKE @site_scope");
            cmd.Parameters.AddWithValue("@site_scope", $"gemini://{query.SiteScope}/%");
        }

        if (query.HasFileTypeScope)
        {
            filters.Add("COALESCE(u.LastMimeType, '') LIKE @filetype_scope");
            cmd.Parameters.AddWithValue("@filetype_scope", $"%{query.FileTypeScope}%");
        }

        if (query.HasUrlScope)
        {
            filters.Add("u.NormalizedUrl LIKE @url_scope");
            cmd.Parameters.AddWithValue("@url_scope", $"%{query.UrlScope}%");
        }
    }
}
