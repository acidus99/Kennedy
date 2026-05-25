using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Gemini.Net;
using Kennedy.Search.Query;
using Kennedy.Search.Services;
using Microsoft.Data.Sqlite;

var searchDbPath = "/Users/billy/kennedy-capsule/crawl-data/kennedy2.db";
var archiveDbPath = "/Users/billy/kennedy-capsule/crawl-data/archive.db";
var serverDllPath = "/Users/billy/Code/Kennedy/Server/bin/Debug/net8.0/Kennedy.Server.dll";

// Probe mode is used to validate remote old-Kennedy behavior without requiring local DB files.
if (args.Any(x => string.Equals(x, "--probe-old", StringComparison.OrdinalIgnoreCase)))
{
    return ProbeOldKennedySearch();
}
if (args.Any(x => string.Equals(x, "--compare-old-new", StringComparison.OrdinalIgnoreCase)))
{
    return CompareOldAndNew(searchDbPath, archiveDbPath, serverDllPath);
}
if (args.Any(x => string.Equals(x, "--compare-search-perf", StringComparison.OrdinalIgnoreCase)))
{
    return CompareSearchPerf(searchDbPath, archiveDbPath, serverDllPath);
}

if (!File.Exists(searchDbPath))
{
    Console.Error.WriteLine($"Database not found: {searchDbPath}");
    return 2;
}

if (!File.Exists(archiveDbPath))
{
    Console.Error.WriteLine($"Archive database not found: {archiveDbPath}");
    return 2;
}

if (!File.Exists(serverDllPath))
{
    Console.Error.WriteLine($"Server binary not found: {serverDllPath}");
    return 2;
}

try
{
    var parser = new QueryParser();
    var search = new SqliteSearchService(searchDbPath);

    // Build expected values directly from SQLite/search service first.
    var sample = BuildSampleData(searchDbPath, archiveDbPath, parser, search);
    RunDirectSearchChecks(parser, search, sample);

    // Boot the server and verify routes against those same expected values.
    var serverPort = FindFreePort(1966, 2100);
    var settingsPath = WriteTempSettings(serverPort);
    using var serverProcess = StartServer(serverDllPath, settingsPath);

    try
    {
        WaitForServer(serverPort, serverProcess);
        RunServerRouteTests(serverPort, sample);
    }
    finally
    {
        if (!serverProcess.HasExited)
        {
            serverProcess.Kill(entireProcessTree: true);
            serverProcess.WaitForExit(5000);
        }
    }

    Console.WriteLine("All checks passed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine("TEST FAILURE");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

return 0;

static void RunDirectSearchChecks(QueryParser parser, SqliteSearchService search, SampleData sample)
{
    var textQuery = parser.Parse(sample.TextQuery);
    var textCount = search.GetTextResultsCount(textQuery);
    AssertTrue(textCount == sample.TextCount, $"Text count mismatch. Expected {sample.TextCount}, got {textCount}.");
    var textTop = search.SearchText(textQuery, 0, 1);
    AssertTrue(textTop.Count == 1, "Expected at least one text result.");
    AssertTrue(textTop[0].Url == sample.TextTopUrl, "Top text result mismatch.");
    Console.WriteLine($"PASS direct text search: '{sample.TextQuery}' -> {sample.TextCount}");

    var imageQuery = parser.Parse(sample.ImageQuery);
    var imageCount = search.GetImageResultsCount(imageQuery);
    AssertTrue(imageCount == sample.ImageCount, $"Image count mismatch. Expected {sample.ImageCount}, got {imageCount}.");
    var imageTop = search.SearchImages(imageQuery, 0, 1);
    AssertTrue(imageTop.Count == 1, "Expected at least one image result.");
    AssertTrue(imageTop[0].Url == sample.ImageTopUrl, "Top image result mismatch.");
    Console.WriteLine($"PASS direct image search: '{sample.ImageQuery}' -> {sample.ImageCount}");

    var luckyTop = search.SearchText(parser.Parse(sample.LuckyQuery), 0, 1);
    AssertTrue(luckyTop.Count == 1, "Expected at least one lucky result.");
    AssertTrue(luckyTop[0].Url == sample.LuckyTopUrl, "Lucky top URL mismatch.");
    Console.WriteLine($"PASS direct lucky search source: '{sample.LuckyQuery}' -> {sample.LuckyTopUrl}");
}

static void RunServerRouteTests(int port, SampleData sample)
{
    var requestor = new GeminiRequestor
    {
        ConnectionTimeout = 60000,
        AbortTimeout = 60000,
        MaxResponseSize = 1024 * 1024 * 4
    };

    GeminiResponse Request(string path)
    {
        var url = $"gemini://localhost:{port}{path}";
        return requestor.Request(url);
    }

    void AssertRouteStatus(string name, GeminiResponse response, params int[] allowedStatusCodes)
    {
        if (!allowedStatusCodes.Contains(response.StatusCode))
        {
            throw new ApplicationException(
                $"Route '{name}' failed. Expected [{string.Join(',', allowedStatusCodes)}], got {response.StatusCode} {response.Meta}");
        }
    }

    void AssertNoUnhandledError(string name, GeminiResponse response)
    {
        if (!response.IsSuccess)
        {
            return;
        }

        var body = response.BodyText ?? string.Empty;
        if (body.Contains("An unhandled error occurred while processing this URL.", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationException($"Route '{name}' returned unhandled error body.");
        }
    }

    void AssertContains(string name, GeminiResponse response, string expected)
    {
        var body = response.BodyText ?? string.Empty;
        if (!body.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationException($"Route '{name}' did not contain expected text: {expected}");
        }
    }

    void AssertInput(string name, string path, string expectedPrompt)
    {
        var response = Request(path);
        AssertRouteStatus(name, response, 10);
        var meta = response.Meta ?? string.Empty;
        if (!meta.Contains(expectedPrompt, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationException($"Route '{name}' input meta did not contain expected prompt: {expectedPrompt}");
        }
        Console.WriteLine($"PASS route: {name}");
    }

    void AssertSuccess(string name, string path, params string[] expectedContent)
    {
        var response = Request(path);
        AssertRouteStatus(name, response, 20);
        AssertNoUnhandledError(name, response);
        foreach (var expected in expectedContent)
        {
            AssertContains(name, response, expected);
        }

        Console.WriteLine($"PASS route: {name}");
    }

    void AssertRedirect(string name, string path, string? redirectMustContain = null)
    {
        var response = Request(path);
        AssertRouteStatus(name, response, 30, 31);
        if (!string.IsNullOrWhiteSpace(redirectMustContain) &&
            (response.Meta?.Contains(redirectMustContain, StringComparison.OrdinalIgnoreCase) != true))
        {
            throw new ApplicationException($"Route '{name}' redirect meta did not contain: {redirectMustContain}");
        }

        Console.WriteLine($"PASS route: {name}");
    }

    // Search routes with DB-backed expectations.
    AssertInput("search input", "/search", "Enter search query");
    AssertSuccess(
        "search results (site scope)",
        $"/search?{Uri.EscapeDataString(sample.TextQuery)}",
        sample.TextTopUrl,
        $"of {FormatCount(sample.TextCount)} results");
    AssertSuccess(
        "search results (filetype scope)",
        $"/search?{Uri.EscapeDataString(sample.FileTypeQuery)}",
        $"of {FormatCount(sample.FileTypeCount)} results");
    AssertSuccess(
        "search stats",
        "/stats",
        $"Active Capsules: {FormatCount(sample.ActiveCapsules)}",
        $"Total Urls: {FormatCount(sample.TotalUrls)}",
        $"Documents: {FormatCount(sample.Documents)}");
    AssertRedirect("lucky", $"/lucky?{Uri.EscapeDataString(sample.LuckyQuery)}", sample.LuckyTopUrl);
    AssertInput("site search create input", "/site-search/create", "Enter domain name");
    AssertSuccess("site search create", $"/site-search/create?{sample.SiteSearchDomain}", "Kennedy Site Search");
    AssertInput("site search run input", $"/site-search/s/{sample.SiteSearchDomain}/", $"Search '{sample.SiteSearchDomain}'");
    AssertRedirect("site search run redirect", $"/site-search/s/{sample.SiteSearchDomain}/?cat", "/search?site");
    AssertInput("image search input", "/image-search", "Enter image search query");
    AssertSuccess(
        "image search results",
        $"/image-search?{Uri.EscapeDataString(sample.ImageQuery)}",
        sample.ImageTopUrl,
        $"of {FormatCount(sample.ImageCount)} results");

    // Archive routes.
    AssertInput("archive search input", "/archive/search", "Search for URLs containing");
    AssertSuccess(
        "archive search by partial",
        $"/archive/search?{Uri.EscapeDataString(sample.ArchiveSearchQuery)}",
        $"Found {FormatCount(sample.ArchiveSearchCount)} urls matching query",
        sample.ArchiveSearchTopUrl);
    AssertRedirect(
        "archive search redirect by full url",
        $"/archive/search?{Uri.EscapeDataString(sample.ArchiveTextUrl)}",
        "/archive/history?");
    AssertInput("archive history input", "/archive/history", "Enter specific URL");
    AssertSuccess(
        "archive history",
        $"/archive/history?{Uri.EscapeDataString(sample.ArchiveTextUrl)}",
        $"Unique snapshots: {sample.ArchiveUniqueSnapshots}");
    AssertSuccess(
        "archive full history",
        $"/archive/history-all?{Uri.EscapeDataString(sample.ArchiveTextUrl)}",
        $"Showing all {sample.ArchiveTotalSnapshots} snapshots");
    AssertSuccess(
        "archive cached",
        $"/archive/cached?url={Uri.EscapeDataString(sample.ArchiveTextUrl)}&t={sample.ArchiveTextSnapshotTicks}",
        "Archived View");
    AssertSuccess(
        "archive diff history",
        $"/archive/diff-history?{Uri.EscapeDataString(sample.DiffUrl)}",
        "Differences");
    AssertSuccess(
        "archive diff",
        $"/archive/diff?url={Uri.EscapeDataString(sample.DiffUrl)}&pt={sample.DiffPreviousTicks}&t={sample.DiffCurrentTicks}",
        "Differences View");
    AssertSuccess("archive stats", "/archive/stats", "Archive Statistics");

    // Reports / tools / info routes with DB-driven checks where possible.
    AssertInput("cert check input", "/certs/validator/check", "URL or Domain to check");
    AssertSuccess("cert check", $"/certs/validator/check?localhost:{port}", "Certificate and Key Validator");
    AssertInput("domain backlinks input", "/reports/domain-backlinks", "Enter Domain");
    AssertSuccess(
        "domain backlinks",
        $"/reports/domain-backlinks?{Uri.EscapeDataString(sample.BacklinksAuthority)}",
        $"Backlinks: {sample.BacklinksCount}");
    AssertInput("site health input", "/reports/site-health", "Enter Domain");
    AssertSuccess(
        "site health",
        $"/reports/site-health?{Uri.EscapeDataString(sample.SiteHealthDomain)}",
        $"Total URLs: {sample.SiteHealthTotal}");
    AssertInput("url info input", "/page-info", "Entry URL");
    AssertSuccess("url info", $"/page-info?{Uri.EscapeDataString(sample.UrlInfoUrl)}", sample.UrlInfoUrl, "Metadata");
    AssertInput("robots tester input", "/tools/robots-tester", "Entry domain to test");
    AssertSuccess("robots tester invalid", "/tools/robots-tester?not_a_domain", "Invalid Gemini URL");
    AssertInput("url tester input", "/tools/url-tester", "Entry URL to test");
    AssertSuccess("url tester", $"/tools/url-tester?{Uri.EscapeDataString($"gemini://localhost:{port}/search?cat")}", "URL Checker");
    AssertSuccess("known hosts", "/observatory/known-hosts", $"Known Capsules ({sample.KnownCapsulesCount})");
    AssertSuccess("security txt", "/observatory/security.txt", $"Capsules with security.txt ({sample.SecurityTxtCount})");

    // Legacy compatibility redirects.
    AssertRedirect("legacy delorean redirect", "/delorean", "/archive/");
    AssertRedirect("legacy cached redirect", "/cached", "/archive/");
    AssertRedirect("legacy mentions redirect", "/mentions/", "/mentions-and-hashtags.gmi");
    AssertRedirect("legacy hashtags redirect", "/hashtags/", "/mentions-and-hashtags.gmi");
}

static SampleData BuildSampleData(
    string searchDbPath,
    string archiveDbPath,
    QueryParser parser,
    SqliteSearchService search)
{
    var sample = new SampleData();

    sample.TextQuery = "site:gemi.dev cat";
    sample.TextCount = search.GetTextResultsCount(parser.Parse(sample.TextQuery));
    AssertTrue(sample.TextCount > 0, "Expected at least one text result for sample query.");
    sample.TextTopUrl = search.SearchText(parser.Parse(sample.TextQuery), 0, 1)[0].Url;

    sample.ImageQuery = "cat";
    sample.ImageCount = search.GetImageResultsCount(parser.Parse(sample.ImageQuery));
    AssertTrue(sample.ImageCount > 0, "Expected at least one image result for sample query.");
    sample.ImageTopUrl = search.SearchImages(parser.Parse(sample.ImageQuery), 0, 1)[0].Url;

    sample.LuckyQuery = "cat";
    sample.LuckyTopUrl = search.SearchText(parser.Parse(sample.LuckyQuery), 0, 1)[0].Url;

    // Use one mime fragment that is guaranteed to exist for filetype route checks.
    using (var searchConn = new SqliteConnection($"Data Source={searchDbPath}"))
    {
        searchConn.Open();

        sample.TotalUrls = ExecuteLongScalar(searchConn, "SELECT COUNT(*) FROM UrlRegistry;");
        sample.Documents = ExecuteLongScalar(searchConn, "SELECT COUNT(*) FROM Documents;");
        sample.ActiveCapsules = ExecuteLongScalar(
            searchConn,
            $"SELECT COUNT(*) FROM (SELECT DISTINCT Scheme, Host, Port FROM UrlRegistry WHERE LastStatusCode != {GeminiParser.ConnectionErrorStatusCode} OR LastStatusCode IS NULL);");

        sample.KnownCapsulesCount = ExecuteLongScalar(
            searchConn,
            $"SELECT COUNT(*) FROM (SELECT 1 FROM UrlRegistry WHERE LastStatusCode != {GeminiParser.ConnectionErrorStatusCode} OR LastStatusCode IS NULL GROUP BY Scheme, Host, Port);");

        sample.SecurityTxtCount = ExecuteLongScalar(
            searchConn,
            "SELECT COUNT(*) FROM (SELECT 1 FROM UrlRegistry WHERE PathAndQuery LIKE '/.well-known/security.txt%' AND LastStatusCode >= 20 AND LastStatusCode < 30 GROUP BY Host, Port);");

        sample.SiteHealthDomain = ExecuteStringScalar(
            searchConn,
            "SELECT Host FROM UrlRegistry WHERE Host <> '' GROUP BY Host ORDER BY COUNT(*) DESC LIMIT 1;")
            ?? throw new ApplicationException("Could not find sample domain for site health.");
        sample.SiteHealthTotal = (int)ExecuteLongScalar(
            searchConn,
            "SELECT COUNT(*) FROM UrlRegistry WHERE Host = $host;",
            ("$host", sample.SiteHealthDomain));

        sample.BacklinksAuthority = ReadBacklinkAuthority(searchConn, out var backlinksCount);
        sample.BacklinksCount = backlinksCount;

        sample.UrlInfoUrl = ExecuteStringScalar(
            searchConn,
            "SELECT CanonicalUrl FROM Documents WHERE IsSearchable = 1 ORDER BY LastIndexedUtc DESC LIMIT 1;")
            ?? throw new ApplicationException("Could not find searchable URL for page-info.");

        sample.SiteSearchDomain = new GeminiUrl(sample.UrlInfoUrl).Hostname;

        var fileTypeFragment = ExecuteStringScalar(
            searchConn,
            "SELECT lower(substr(MimeType, 1, instr(MimeType || '/', '/') - 1)) FROM Documents WHERE MimeType IS NOT NULL AND MimeType <> '' LIMIT 1;")
            ?? "text";
        sample.FileTypeQuery = $"filetype:{fileTypeFragment} cat";
        sample.FileTypeCount = search.GetTextResultsCount(parser.Parse(sample.FileTypeQuery));
    }

    using (var archiveConn = new SqliteConnection($"Data Source={archiveDbPath}"))
    {
        archiveConn.Open();

        // Pick a URL with at least one successful text snapshot so cached view can be asserted on body output.
        using (var cmd = archiveConn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    u.FullUrl,
    (SELECT s.Captured FROM Snapshots s
        WHERE s.UrlId = u.Id AND s.Mimetype LIKE 'text/%' AND s.StatusCode >= 20 AND s.StatusCode < 30
        ORDER BY s.Captured DESC LIMIT 1) AS LatestCaptured,
    (SELECT COUNT(*) FROM Snapshots s WHERE s.UrlId = u.Id) AS TotalSnapshots,
    (SELECT COUNT(*) FROM Snapshots s WHERE s.UrlId = u.Id AND s.IsDuplicate = 0) AS UniqueSnapshots
FROM Urls u
WHERE u.IsPublic = 1
  AND EXISTS (
      SELECT 1 FROM Snapshots s
      WHERE s.UrlId = u.Id AND s.Mimetype LIKE 'text/%' AND s.StatusCode >= 20 AND s.StatusCode < 30
  )
ORDER BY u.Id
LIMIT 1;";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new ApplicationException("Could not find a public archive URL with a successful text snapshot.");
            }

            sample.ArchiveTextUrl = reader.GetString(0);
            sample.ArchiveTextSnapshotTicks = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture).Ticks;
            sample.ArchiveTotalSnapshots = reader.GetInt32(2);
            sample.ArchiveUniqueSnapshots = reader.GetInt32(3);
        }

        sample.ArchiveSearchQuery = new GeminiUrl(sample.ArchiveTextUrl).Hostname;
        sample.ArchiveSearchCount = (int)ExecuteLongScalar(
            archiveConn,
            "SELECT COUNT(*) FROM Urls WHERE IsPublic = 1 AND instr(FullUrl, $q) > 0;",
            ("$q", sample.ArchiveSearchQuery));
        sample.ArchiveSearchTopUrl = ExecuteStringScalar(
            archiveConn,
            "SELECT FullUrl FROM Urls WHERE IsPublic = 1 AND instr(FullUrl, $q) > 0 ORDER BY instr(FullUrl, $q), length(FullUrl), FullUrl LIMIT 1;",
            ("$q", sample.ArchiveSearchQuery))
            ?? throw new ApplicationException("Could not find archive top URL for partial search query.");

        // Pick a diff URL with at least two unique snapshots.
        using (var diffCmd = archiveConn.CreateCommand())
        {
            diffCmd.CommandText = @"
SELECT
    u.FullUrl,
    (SELECT s.Captured FROM Snapshots s
        WHERE s.UrlId = u.Id AND s.IsDuplicate = 0
        ORDER BY s.Captured LIMIT 1) AS PreviousCaptured,
    (SELECT s.Captured FROM Snapshots s
        WHERE s.UrlId = u.Id AND s.IsDuplicate = 0
        ORDER BY s.Captured LIMIT 1 OFFSET 1) AS CurrentCaptured
FROM Urls u
WHERE u.IsPublic = 1
  AND (SELECT COUNT(*) FROM Snapshots s WHERE s.UrlId = u.Id AND s.IsDuplicate = 0) >= 2
LIMIT 1;";

            using var diffReader = diffCmd.ExecuteReader();
            if (!diffReader.Read())
            {
                throw new ApplicationException("Could not find a public archive URL with at least 2 unique snapshots.");
            }

            sample.DiffUrl = diffReader.GetString(0);
            sample.DiffPreviousTicks = DateTime.Parse(diffReader.GetString(1), CultureInfo.InvariantCulture).Ticks;
            sample.DiffCurrentTicks = DateTime.Parse(diffReader.GetString(2), CultureInfo.InvariantCulture).Ticks;
        }
    }

    return sample;
}

static string ReadBacklinkAuthority(SqliteConnection searchConn, out int backlinksCount)
{
    using var cmd = searchConn.CreateCommand();
    cmd.CommandText = @"
SELECT target.Host, target.Port, COUNT(*) AS Cnt
FROM UrlLinks links
JOIN UrlRegistry target ON links.TargetUrlId = target.Id
WHERE links.IsExternal = 1
GROUP BY target.Host, target.Port
ORDER BY Cnt DESC
LIMIT 1;";

    using var reader = cmd.ExecuteReader();
    if (!reader.Read())
    {
        throw new ApplicationException("Could not find any external backlinks in UrlLinks.");
    }

    var host = reader.GetString(0);
    var port = reader.GetInt32(1);
    backlinksCount = reader.GetInt32(2);

    return (port == 1965) ? host : $"{host}:{port}";
}

static long ExecuteLongScalar(SqliteConnection connection, string sql, params (string name, object value)[] parameters)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    foreach (var parameter in parameters)
    {
        command.Parameters.AddWithValue(parameter.name, parameter.value);
    }

    return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
}

static string? ExecuteStringScalar(SqliteConnection connection, string sql, params (string name, object value)[] parameters)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    foreach (var parameter in parameters)
    {
        command.Parameters.AddWithValue(parameter.name, parameter.value);
    }

    return command.ExecuteScalar()?.ToString();
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new ApplicationException(message);
    }
}

static int FindFreePort(int start, int end)
{
    for (int port = start; port <= end; port++)
    {
        try
        {
            var listener = System.Net.Sockets.TcpListener.Create(port);
            listener.Start();
            listener.Stop();
            return port;
        }
        catch
        {
        }
    }

    throw new ApplicationException("Could not find an open port for integration tests.");
}

static string WriteTempSettings(int port)
{
    var settings = new
    {
        Settings = new
        {
            Host = "localhost",
            Port = port,
            CertificateFile = "/Users/billy/kennedy-capsule/certs/localhost.crt",
            KeyFile = "/Users/billy/kennedy-capsule/certs/localhost.key",
            PublicRoot = "/Users/billy/kennedy-capsule/public_root",
            AccessLogPath = "",
            DataRoot = "/Users/billy/kennedy-capsule/crawl-data/"
        }
    };

    var path = Path.Combine(Path.GetTempPath(), $"kennedy-tests.{port}.json");
    File.WriteAllText(path, JsonSerializer.Serialize(settings));
    return path;
}

static Process StartServer(string serverDllPath, string settingsPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"\"{serverDllPath}\" \"{settingsPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    var process = Process.Start(startInfo)
                  ?? throw new ApplicationException("Failed to start Kennedy.Server process.");

    process.OutputDataReceived += (_, args) =>
    {
        if (!string.IsNullOrEmpty(args.Data))
        {
            Console.WriteLine($"[server] {args.Data}");
        }
    };
    process.ErrorDataReceived += (_, args) =>
    {
        if (!string.IsNullOrEmpty(args.Data))
        {
            Console.WriteLine($"[server-err] {args.Data}");
        }
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    return process;
}

static void WaitForServer(int port, Process serverProcess)
{
    var requestor = new GeminiRequestor
    {
        ConnectionTimeout = 2000,
        AbortTimeout = 2000
    };

    for (int i = 0; i < 30; i++)
    {
        if (serverProcess.HasExited)
        {
            throw new ApplicationException($"Server process exited early with code {serverProcess.ExitCode}.");
        }

        var response = requestor.Request($"gemini://localhost:{port}/search");
        if (response.StatusCode is 10 or 20 or 30 or 31)
        {
            return;
        }

        Thread.Sleep(300);
    }

    throw new ApplicationException($"Server did not become ready on port {port}.");
}

static string FormatCount(long i)
    => i.ToString("N0", CultureInfo.InvariantCulture);

static int ProbeOldKennedySearch()
{
    const string url = "gemini://kennedy.gemi.dev/search?cat";

    var requestor = new GeminiRequestor
    {
        ConnectionTimeout = 60000,
        AbortTimeout = 60000,
        MaxResponseSize = 1024 * 1024 * 4
    };

    var response = requestor.Request(url);
    Console.WriteLine($"Probe URL: {url}");
    Console.WriteLine($"Status: {response.StatusCode}");
    Console.WriteLine($"Meta: {response.Meta}");

    if (!(response.StatusCode is 20 or 30 or 31))
    {
        Console.Error.WriteLine("Probe failed: expected success or redirect response from old Kennedy search.");
        return 1;
    }

    var body = response.BodyText ?? string.Empty;
    Console.WriteLine($"Body length: {body.Length}");

    if (response.StatusCode == 20 && body.Length == 0)
    {
        Console.Error.WriteLine("Probe failed: got 20 but empty body for old Kennedy search.");
        return 1;
    }

    Console.WriteLine("PASS old-kennedy probe: received response for search query 'cat'.");
    return 0;
}

static int CompareOldAndNew(string searchDbPath, string archiveDbPath, string serverDllPath)
{
    if (!File.Exists(searchDbPath) || !File.Exists(archiveDbPath) || !File.Exists(serverDllPath))
    {
        Console.Error.WriteLine("compare-old-new requires local search DB, archive DB, and server build artifacts.");
        return 2;
    }

    var parser = new QueryParser();
    var search = new SqliteSearchService(searchDbPath);
    var sample = BuildSampleData(searchDbPath, archiveDbPath, parser, search);

    var serverPort = FindFreePort(1966, 2100);
    var settingsPath = WriteTempSettings(serverPort);
    using var serverProcess = StartServer(serverDllPath, settingsPath);

    try
    {
        WaitForServer(serverPort, serverProcess);

        var oldBase = "gemini://kennedy.gemi.dev";
        var newBase = $"gemini://localhost:{serverPort}";

        var paths = new List<(string Name, string Path)>
        {
            ("search input", "/search"),
            ("search cat", "/search?cat"),
            ("search site scope", "/search?site:gemi.dev+cat"),
            ("search filetype", "/search?filetype:text+cat"),
            ("search stats", "/stats"),
            ("lucky cat", "/lucky?cat"),
            ("site-search create input", "/site-search/create"),
            ("site-search create", "/site-search/create?gemi.dev"),
            ("site-search run input", "/site-search/s/gemi.dev/"),
            ("site-search run", "/site-search/s/gemi.dev/?cat"),
            ("image search input", "/image-search"),
            ("image search cat", "/image-search?cat"),
            ("archive search input", "/archive/search"),
            ("archive search", "/archive/search?gemi.dev"),
            ("archive history input", "/archive/history"),
            ("archive history", $"/archive/history?{Uri.EscapeDataString(sample.ArchiveTextUrl)}"),
            ("archive full history", $"/archive/history-all?{Uri.EscapeDataString(sample.ArchiveTextUrl)}"),
            ("archive cached", $"/archive/cached?url={Uri.EscapeDataString(sample.ArchiveTextUrl)}&t={sample.ArchiveTextSnapshotTicks}"),
            ("archive diff history", $"/archive/diff-history?{Uri.EscapeDataString(sample.DiffUrl)}"),
            ("archive diff", $"/archive/diff?url={Uri.EscapeDataString(sample.DiffUrl)}&pt={sample.DiffPreviousTicks}&t={sample.DiffCurrentTicks}"),
            ("archive stats", "/archive/stats"),
            ("cert check input", "/certs/validator/check"),
            ("cert check", "/certs/validator/check?gemi.dev"),
            ("domain backlinks input", "/reports/domain-backlinks"),
            ("domain backlinks", "/reports/domain-backlinks?gemi.dev"),
            ("site health input", "/reports/site-health"),
            ("site health", "/reports/site-health?gemi.dev"),
            ("url info input", "/page-info"),
            ("url info", $"/page-info?{Uri.EscapeDataString(sample.UrlInfoUrl)}"),
            ("robots tester input", "/tools/robots-tester"),
            ("robots tester", "/tools/robots-tester?gemi.dev"),
            ("url tester input", "/tools/url-tester"),
            ("url tester", $"/tools/url-tester?{Uri.EscapeDataString("gemini://gemi.dev/")}"),
            ("known hosts", "/observatory/known-hosts"),
            ("security txt", "/observatory/security.txt"),
            ("legacy delorean", "/delorean"),
            ("legacy cached", "/cached"),
            ("legacy mentions", "/mentions/"),
            ("legacy hashtags", "/hashtags/")
        };

        var requestor = new GeminiRequestor
        {
            ConnectionTimeout = 60000,
            AbortTimeout = 60000,
            MaxResponseSize = 1024 * 1024 * 8
        };

        var rows = new List<(string Name, ProbeResult Old, ProbeResult New)>();

        foreach (var route in paths)
        {
            var oldResult = RequestTimed(requestor, oldBase + route.Path);
            var newResult = RequestTimed(requestor, newBase + route.Path);
            rows.Add((route.Name, oldResult, newResult));
        }

        Console.WriteLine("=== Route Comparison (Old vs New) ===");
        foreach (var row in rows)
        {
            Console.WriteLine(
                $"{row.Name}|old:{row.Old.StatusCode} {TrimForPrint(row.Old.Meta)} {row.Old.ElapsedMs}ms first={TrimForPrint(row.Old.FirstLine)} len={row.Old.BodyLength}|new:{row.New.StatusCode} {TrimForPrint(row.New.Meta)} {row.New.ElapsedMs}ms first={TrimForPrint(row.New.FirstLine)} len={row.New.BodyLength}");
            if (row.Old.QueryTimeMs != null || row.New.QueryTimeMs != null)
            {
                Console.WriteLine($"  query_ms old={row.Old.QueryTimeMs?.ToString() ?? "-"} new={row.New.QueryTimeMs?.ToString() ?? "-"}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Mismatches ===");
        foreach (var row in rows)
        {
            if (row.Old.StatusCode != row.New.StatusCode)
            {
                Console.WriteLine($"STATUS: {row.Name} old={row.Old.StatusCode} new={row.New.StatusCode}");
            }
            else if (row.Old.StatusCode == 20 &&
                     !string.Equals(row.Old.FirstLine, row.New.FirstLine, StringComparison.Ordinal))
            {
                Console.WriteLine($"FIRST-LINE: {row.Name} old='{TrimForPrint(row.Old.FirstLine)}' new='{TrimForPrint(row.New.FirstLine)}'");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Performance (avg of 5) ===");
        var perfPaths = new List<(string Name, string Path)>
        {
            ("search cat", "/search?cat"),
            ("search site scope", "/search?site:gemi.dev+cat"),
            ("search filetype", "/search?filetype:text+cat"),
            ("image search cat", "/image-search?cat"),
            ("domain backlinks", "/reports/domain-backlinks?gemi.dev"),
            ("known hosts", "/observatory/known-hosts")
        };

        foreach (var perf in perfPaths)
        {
            var oldAvg = AverageMs(requestor, oldBase + perf.Path, 5);
            var newAvg = AverageMs(requestor, newBase + perf.Path, 5);
            var ratio = (oldAvg > 0.001) ? (newAvg / oldAvg) : 0;
            var oldQueryAvg = AverageQueryMs(requestor, oldBase + perf.Path, 5);
            var newQueryAvg = AverageQueryMs(requestor, newBase + perf.Path, 5);
            Console.WriteLine($"{perf.Name}|old_full_avg_ms={oldAvg:F1}|new_full_avg_ms={newAvg:F1}|new_vs_old_full={ratio:F2}x|old_query_avg_ms={(oldQueryAvg?.ToString("F1", CultureInfo.InvariantCulture) ?? "-")}|new_query_avg_ms={(newQueryAvg?.ToString("F1", CultureInfo.InvariantCulture) ?? "-")}");
        }

        return 0;
    }
    finally
    {
        if (!serverProcess.HasExited)
        {
            serverProcess.Kill(entireProcessTree: true);
            serverProcess.WaitForExit(5000);
        }
    }
}

static int CompareSearchPerf(string searchDbPath, string archiveDbPath, string serverDllPath)
{
    if (!File.Exists(searchDbPath) || !File.Exists(archiveDbPath) || !File.Exists(serverDllPath))
    {
        Console.Error.WriteLine("compare-search-perf requires local search DB, archive DB, and server build artifacts.");
        return 2;
    }

    var parser = new QueryParser();
    var search = new SqliteSearchService(searchDbPath);
    _ = BuildSampleData(searchDbPath, archiveDbPath, parser, search);

    var serverPort = FindFreePort(1966, 2100);
    var settingsPath = WriteTempSettings(serverPort);
    using var serverProcess = StartServer(serverDllPath, settingsPath);

    try
    {
        WaitForServer(serverPort, serverProcess);

        var oldBase = "gemini://kennedy.gemi.dev";
        var newBase = $"gemini://localhost:{serverPort}";

        var requestor = new GeminiRequestor
        {
            ConnectionTimeout = 60000,
            AbortTimeout = 60000,
            MaxResponseSize = 1024 * 1024 * 8
        };

        var routes = new List<(string Name, string Path)>
        {
            ("search cat", "/search?cat"),
            ("search site scope", "/search?site:gemi.dev+cat"),
            ("search filetype", "/search?filetype:text+cat"),
            ("image search cat", "/image-search?cat")
        };

        Console.WriteLine("=== Search Perf (full response + backend query line) ===");
        foreach (var route in routes)
        {
            var oldFull = AverageMs(requestor, oldBase + route.Path, 5);
            var newFull = AverageMs(requestor, newBase + route.Path, 5);
            var oldQuery = AverageQueryMs(requestor, oldBase + route.Path, 5);
            var newQuery = AverageQueryMs(requestor, newBase + route.Path, 5);
            var ratio = (oldFull > 0.001) ? (newFull / oldFull) : 0;

            Console.WriteLine(
                $"{route.Name}|old_full_avg_ms={oldFull:F1}|new_full_avg_ms={newFull:F1}|new_vs_old_full={ratio:F2}x|old_query_avg_ms={(oldQuery?.ToString("F1", CultureInfo.InvariantCulture) ?? "-")}|new_query_avg_ms={(newQuery?.ToString("F1", CultureInfo.InvariantCulture) ?? "-")}");
        }

        return 0;
    }
    finally
    {
        if (!serverProcess.HasExited)
        {
            serverProcess.Kill(entireProcessTree: true);
            serverProcess.WaitForExit(5000);
        }
    }
}

static ProbeResult RequestTimed(GeminiRequestor requestor, string url)
{
    var sw = Stopwatch.StartNew();
    var response = requestor.Request(url);
    sw.Stop();

    var body = response.BodyText ?? string.Empty;
    var firstLine = body
        .Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault() ?? string.Empty;

    int? queryTimeMs = null;
    if (!string.IsNullOrWhiteSpace(body))
    {
        var match = Regex.Match(body, @"Query time:\s*(\d+)\s*ms", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
        {
            queryTimeMs = parsed;
        }
    }

    return new ProbeResult
    {
        Url = url,
        StatusCode = response.StatusCode,
        Meta = response.Meta ?? string.Empty,
        BodyLength = body.Length,
        FirstLine = firstLine,
        ElapsedMs = sw.Elapsed.TotalMilliseconds,
        QueryTimeMs = queryTimeMs
    };
}

static double AverageMs(GeminiRequestor requestor, string url, int count)
{
    var total = 0d;
    for (int i = 0; i < count; i++)
    {
        total += RequestTimed(requestor, url).ElapsedMs;
    }

    return total / count;
}

static double? AverageQueryMs(GeminiRequestor requestor, string url, int count)
{
    var values = new List<double>();
    for (int i = 0; i < count; i++)
    {
        var result = RequestTimed(requestor, url);
        if (result.QueryTimeMs != null)
        {
            values.Add(result.QueryTimeMs.Value);
        }
    }

    if (values.Count == 0)
    {
        return null;
    }

    return values.Average();
}

static string TrimForPrint(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "";
    }

    value = value.Replace("|", "/");
    if (value.Length > 120)
    {
        return value.Substring(0, 120) + "...";
    }

    return value;
}

internal sealed class ProbeResult
{
    public required string Url { get; init; }
    public required int StatusCode { get; init; }
    public required string Meta { get; init; }
    public required int BodyLength { get; init; }
    public required string FirstLine { get; init; }
    public required double ElapsedMs { get; init; }
    public int? QueryTimeMs { get; init; }
}

internal sealed class SampleData
{
    public string TextQuery { get; set; } = "";
    public int TextCount { get; set; }
    public string TextTopUrl { get; set; } = "";

    public string ImageQuery { get; set; } = "";
    public int ImageCount { get; set; }
    public string ImageTopUrl { get; set; } = "";

    public string LuckyQuery { get; set; } = "";
    public string LuckyTopUrl { get; set; } = "";

    public string FileTypeQuery { get; set; } = "";
    public int FileTypeCount { get; set; }

    public long ActiveCapsules { get; set; }
    public long TotalUrls { get; set; }
    public long Documents { get; set; }

    public string SiteSearchDomain { get; set; } = "";
    public string UrlInfoUrl { get; set; } = "";

    public string SiteHealthDomain { get; set; } = "";
    public int SiteHealthTotal { get; set; }

    public string BacklinksAuthority { get; set; } = "";
    public int BacklinksCount { get; set; }

    public long KnownCapsulesCount { get; set; }
    public long SecurityTxtCount { get; set; }

    public string ArchiveSearchQuery { get; set; } = "";
    public int ArchiveSearchCount { get; set; }
    public string ArchiveSearchTopUrl { get; set; } = "";

    public string ArchiveTextUrl { get; set; } = "";
    public long ArchiveTextSnapshotTicks { get; set; }
    public int ArchiveTotalSnapshots { get; set; }
    public int ArchiveUniqueSnapshots { get; set; }

    public string DiffUrl { get; set; } = "";
    public long DiffPreviousTicks { get; set; }
    public long DiffCurrentTicks { get; set; }
}
