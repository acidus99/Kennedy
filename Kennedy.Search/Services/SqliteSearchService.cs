using Kennedy.Search.Models;
using Microsoft.Data.Sqlite;

namespace Kennedy.Search.Services;

/// <summary>Read-only queries over current text, file, and image search state.</summary>
public sealed class SqliteSearchService : ISearchService
{
    private readonly string _connectionString;
    public SqliteSearchService(string sqlitePath)
        => _connectionString = new SqliteConnectionStringBuilder { DataSource=sqlitePath, Mode=SqliteOpenMode.ReadOnly }.ToString();

    public int GetTextResultsCount(UserQuery query) => Count(query, false);
    public int GetImageResultsCount(UserQuery query) => Count(query, true);

    private int Count(UserQuery query, bool images)
    {
        Validate(query, images);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM (" + Candidates(query, command, images, true) + ");";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyList<TextSearchResult> SearchText(UserQuery query, int offset, int limit)
    {
        Validate(query, false);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "WITH hits AS MATERIALIZED (" + Candidates(query, command, false, false) +
            ") SELECT * FROM hits ORDER BY Score, Captured DESC, Url LIMIT @limit OFFSET @offset;";
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit,1,100));
        command.Parameters.AddWithValue("@offset", Math.Max(0,offset));
        using var reader = command.ExecuteReader();
        var results = new List<TextSearchResult>();
        while (reader.Read()) results.Add(new TextSearchResult
        {
            Url=reader.GetString(0), Title=String(reader,1), MimeType=String(reader,2),
            DetectedLanguage=String(reader,3), LineCount=reader.IsDBNull(4)?null:reader.GetInt32(4),
            BodySize=reader.GetInt32(5), IsBodyTruncated=reader.GetBoolean(6), Favicon=String(reader,7),
            Snippet=ReadSnippet(connection,query,reader.IsDBNull(14),reader.IsDBNull(14)?reader.GetInt64(15):reader.GetInt64(14)), LastSuccess=reader.IsDBNull(9)?null:reader.GetDateTime(9)
        });
        return results;
    }

    public IReadOnlyList<ImageSearchResult> SearchImages(UserQuery query, int offset, int limit)
    {
        Validate(query, true);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "WITH hits AS MATERIALIZED (" + Candidates(query, command, true, false) +
            ") SELECT * FROM hits ORDER BY Captured DESC, Url LIMIT @limit OFFSET @offset;";
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit,1,100));
        command.Parameters.AddWithValue("@offset", Math.Max(0,offset));
        using var reader = command.ExecuteReader();
        var results = new List<ImageSearchResult>();
        while (reader.Read()) results.Add(new ImageSearchResult
        {
            Url=reader.GetString(0), BodySize=reader.GetInt32(5), IsBodyTruncated=reader.GetBoolean(6),
            Favicon=String(reader,7), Snippet=ReadSnippet(connection,query,true,reader.GetInt64(15)), ImageType=String(reader,11),
            Width=reader.GetInt32(12), Height=reader.GetInt32(13)
        });
        return results;
    }

    // Snippet generation reads document bodies. Do it only for the selected page,
    // after ranking and pagination, rather than for every matching document.
    private static string ReadSnippet(SqliteConnection connection, UserQuery query, bool file, long id)
    {
        string table=file ? "FilesFts" : "DocumentsFts";
        string column=file ? "SearchText" : "Content";
        using var command=connection.CreateCommand();
        command.CommandText=query.HasFtsQuery
            ? $"SELECT snippet({table},1,'[',']','…',20) FROM {table} WHERE rowid=@id AND {table} MATCH @terms;"
            : $"SELECT substr({column},1,180) FROM {table} WHERE rowid=@id;";
        command.Parameters.AddWithValue("@id",id);
        if(query.HasFtsQuery) command.Parameters.AddWithValue("@terms",query.FtsQuery!);
        return command.ExecuteScalar() as string ?? "";
    }

    private static string? String(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void Validate(UserQuery query, bool images)
    {
        if (images ? !query.IsValidImageQuery : !query.IsValidTextQuery)
            throw new ArgumentException("Not a valid search query.");
    }

    private const string PathSql = """
        CASE WHEN instr(substr(u.NormalizedUrl,instr(u.NormalizedUrl,'://')+3),'/')=0 THEN '/'
        ELSE substr(u.NormalizedUrl,instr(u.NormalizedUrl,'://')+2+instr(substr(u.NormalizedUrl,instr(u.NormalizedUrl,'://')+3),'/')) END
        """;

    private static string Candidates(UserQuery query, SqliteCommand command, bool images, bool count)
    {
        var common = new List<string> { "u.Status IN (1,2,3)", "u.IsRobotsExcluded=0" };
        if (query.HasSiteScope)
        {
            common.Add("u.Host=@site");
            command.Parameters.AddWithValue("@site",query.SiteScope!);
        }
        if (query.HasFileTypeScope)
        {
            // Match a filename extension, excluding query strings, as documented.
            common.Add($"LOWER(CASE WHEN instr(({PathSql}),'?')>0 THEN substr(({PathSql}),1,instr(({PathSql}),'?')-1) ELSE ({PathSql}) END) LIKE @extension ESCAPE '\\'");
            command.Parameters.AddWithValue("@extension", "%" + EscapeLike("." + query.FileTypeScope!.TrimStart('.').ToLowerInvariant()));
        }
        if (query.HasUrlScope)
        {
            if (query.UrlScope!.Length < 3)
            {
                common.Add($"instr(lower(({PathSql})),@url)>0");
                command.Parameters.AddWithValue("@url",query.UrlScope.ToLowerInvariant());
            }
            else
            {
                common.Add("u.Id IN (SELECT rowid FROM UrlSearch WHERE UrlSearch MATCH @url)");
                command.Parameters.AddWithValue("@url", "\"" + query.UrlScope.Replace("\"", "\"\"") + "\"");
            }
        }
        if (query.HasFtsQuery) command.Parameters.AddWithValue("@terms",query.FtsQuery!);
        if (query.HasTitleScope) command.Parameters.AddWithValue("@title","%"+EscapeLike(query.TitleScope!)+"%");
        string where = string.Join(" AND ",common);
        string faviconJoin = count ? "" : "LEFT JOIN Favicons favicon ON favicon.Scheme=u.Scheme AND favicon.Host=u.Host AND favicon.Port=u.Port";
        string textColumns = count ? "u.Id" : $"""
            d.CanonicalUrl AS Url,d.Title,d.LastMimeType,d.DetectedLanguage,d.LineCount,
            d.BodySize,d.IsBodyTruncated,favicon.Emoji,
            '' AS Snippet,
            d.LastIndexedUtc AS Captured,{(query.HasFtsQuery ? "bm25(DocumentsFts)" : "0.0")} AS Score,
            NULL AS ImageType,0 AS Width,0 AS Height,d.Id AS DocumentId,u.Id AS UrlId
            """;
        string text = $"""
            SELECT {textColumns} FROM Documents d
            JOIN UrlRegistry u ON u.Id=d.UrlRegistryId
            {(query.HasFtsQuery ? "JOIN" : "LEFT JOIN")} DocumentsFts ON DocumentsFts.rowid=d.Id
            {faviconJoin}
            WHERE {where}
            {(query.HasFtsQuery ? "AND DocumentsFts MATCH @terms" : "")}
            {(query.HasTitleScope ? "AND COALESCE(d.Title,'') LIKE @title ESCAPE '\\'" : "")}
            """;
        string fileColumns = count ? "u.Id" : $"""
            u.NormalizedUrl AS Url,NULL AS Title,COALESCE(u.LastMimeType,u.LastDetectedMimeType) AS LastMimeType,
            NULL AS DetectedLanguage,NULL AS LineCount,u.BodySize,u.IsBodyTruncated,favicon.Emoji,
            '' AS Snippet,
            COALESCE(u.LastIndexedVisit,u.LastSuccess) AS Captured,{(query.HasFtsQuery ? "bm25(FilesFts)" : "0.0")} AS Score,
            i.ImageType,COALESCE(i.Width,0) AS Width,COALESCE(i.Height,0) AS Height,NULL AS DocumentId,u.Id AS UrlId
            """;
        string files = $"""
            SELECT {fileColumns} FROM UrlRegistry u
            LEFT JOIN Images i ON i.UrlRegistryId=u.Id
            {(query.HasFtsQuery ? "JOIN" : "LEFT JOIN")} FilesFts ON FilesFts.rowid=u.Id
            {faviconJoin}
            WHERE {where} AND COALESCE(u.LastMimeType,u.LastDetectedMimeType) IS NOT NULL
              AND NOT EXISTS(SELECT 1 FROM Documents d WHERE d.UrlRegistryId=u.Id)
              AND u.NormalizedUrl NOT LIKE '%/robots.txt' AND u.NormalizedUrl NOT LIKE '%/favicon.txt'
              AND u.NormalizedUrl NOT LIKE '%/.well-known/security.txt'
            {(query.HasFtsQuery ? "AND FilesFts MATCH @terms" : "")}
            {(query.HasTitleScope ? "AND 0" : "")}
            {(images ? "AND (i.UrlRegistryId IS NOT NULL OR LOWER(COALESCE(u.LastDetectedMimeType,u.LastMimeType,'')) LIKE 'image/%')" : "")}
            """;
        return images ? files : text + " UNION ALL " + files;
    }

    private static string EscapeLike(string value) => value.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_");
}
