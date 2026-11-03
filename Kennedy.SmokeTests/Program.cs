using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Gemini.Net;

return SmokeTest.Run(args);

internal static class SmokeTest
{
    public static int Run(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        Process? server = null;
        StreamWriter? serverLog = null;
        string? output = null;
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;

        try
        {
            var options = Options.Parse(args);
            output = options.Output ?? Path.Combine(Path.GetTempPath(), $"kennedy-smoke-{Guid.NewGuid():N}");
            Directory.CreateDirectory(output);
            Console.WriteLine($"Responses and logs: {output}");

            var baseUrl = options.BaseUrl;
            if (baseUrl == null)
            {
                int port = options.Port ?? FindPort();
                string settingsPath = WriteSettings(options.Settings!, output, port);
                serverLog = new StreamWriter(Path.Combine(output, "server.log")) { AutoFlush = true };
                server = StartServer(options.Server ?? FindServerDll(), settingsPath, serverLog);
                baseUrl = new Uri($"gemini://localhost:{port}/");
                WaitForServer(server, baseUrl, cancellation.Token);
            }

            Console.WriteLine($"Checking {baseUrl} (search: {options.Query}; capsule: {options.Capsule})");
            var checks = BuildChecks(options);
            var results = new List<CheckResult>();
            foreach (var check in checks)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (check.Network && options.SkipNetworkTools)
                {
                    Console.WriteLine($"SKIP {check.Name} (--skip-network-tools)");
                    continue;
                }

                string url = new Uri(baseUrl, check.Path).AbsoluteUri;
                string responsePath = Path.Combine(output, $"{results.Count + 1:00}-{check.Name}.gmi");
                var watch = Stopwatch.StartNew();
                GeminiResponse? response = null;
                string? error = null;
                try
                {
                    response = Request(url, options.TimeoutSeconds * 1000);
                    File.WriteAllText(responsePath, $"{response.StatusCode} {response.Meta}\n{response.BodyText}");
                    Validate(response, check);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    if (response == null) File.WriteAllText(responsePath, error);
                }

                results.Add(new CheckResult(check.Name, url, error == null, response?.StatusCode,
                    response?.Meta, error, watch.ElapsedMilliseconds));
                Console.WriteLine($"{(error == null ? "PASS" : "FAIL")} {check.Name} ({watch.ElapsedMilliseconds} ms){(error == null ? "" : $": {error}")}");
            }

            int failed = results.Count(r => !r.Passed);
            int skipped = checks.Count - results.Count;
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(
                new { BaseUrl = baseUrl.AbsoluteUri, Passed = results.Count - failed, Failed = failed, Skipped = skipped, Results = results },
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"\n{results.Count - failed} passed, {failed} failed, {skipped} skipped.");
            return failed == 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Smoke test cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not run smoke test: {ex.Message}");
            return 2;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            if (server != null)
            {
                if (!server.HasExited)
                {
                    server.Kill(entireProcessTree: true);
                }
                // Also drain redirected output when the server exited on its own.
                server.WaitForExit();
                server.Dispose();
            }
            serverLog?.Dispose();
            if (output != null) Console.WriteLine($"Logs: {output}");
        }
    }

    private static List<Check> BuildChecks(Options options)
    {
        string query = Uri.EscapeDataString(options.Query);
        string capsule = Uri.EscapeDataString(options.Capsule);
        string url = Uri.EscapeDataString(options.Url);
        return
        [
            new("capsule", "/", ["Kennedy"]),
            new("text-search", $"/search?{query}", ["Kennedy Search"], Results: true),
            new("image-search", $"/image-search?{query}", ["Kennedy Image Search"], Results: true),
            new("page-info", $"/page-info?{url}", ["Metadata", "Visit Current Url"]),
            new("capsule-health", $"/reports/site-health?{capsule}", ["Capsule Health Report", "Total URLs:"]),
            new("backlinks", $"/reports/domain-backlinks?{capsule}", ["Backlinks Report", "Backlinks:"]),
            new("robots-checker", $"/tools/robots-tester?{capsule}", ["Robots.txt Validator", "Testing:", "Connection: ✅", "Status Code: 20 ✅"], Network: true),
            // This tool accepts a bare authority, while the URL checker takes a full URL.
            new("certificate-checker", $"/certs/validator/check?{capsule}", ["Certificate and Key Validator", "Generic fingerprints"], Network: true),
            new("url-checker", $"/tools/url-tester?{url}", ["URL Checker", "Connection: ✅", "Status Code :"], Network: true),
            new("search-stats", "/stats", ["Kennedy Stats", "Documents:"]),
            new("known-capsules", "/observatory/known-hosts", ["Known Capsules ("]),
            new("security-txt", "/observatory/security.txt", ["Capsules with security.txt"]),
            new("favicons", "/observatory/favicon.txt", ["Favicons!", "Capsules with favicon.txt"])
        ];
    }

    private static void Validate(GeminiResponse response, Check check)
    {
        if (response.StatusCode != 20)
            throw new InvalidOperationException($"Expected status 20, got {response.StatusCode} {response.Meta}");
        if (response.IsBodyTruncated)
            throw new InvalidOperationException("Response exceeded the smoke test's 16 MB limit.");
        string body = response.BodyText ?? "";
        if (Regex.IsMatch(body, @"(?im)^#\s*ERROR!|An unhandled error occurred while processing this URL\.|Invalid Gemini URL"))
            throw new InvalidOperationException("Server returned an error page with status 20; see saved response.");
        if (check.Network && Regex.IsMatch(body, @"(?im)^Failed:|^Error establishing connection:|Connection: ❌"))
            throw new InvalidOperationException("Tool could not check the supplied target; see saved response.");
        foreach (string marker in check.Markers)
            if (!body.Contains(marker, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Missing expected content: {marker}");
        if (check.Results && !Regex.IsMatch(body, @"(?m)^=>\s+gemini://\S+\s+[\d,]+\.\s"))
            throw new InvalidOperationException("Search returned no numbered result links.");
    }

    private static GeminiResponse Request(string url, int timeoutMs) => new GeminiRequestor
    {
        ConnectionTimeout = timeoutMs,
        AbortTimeout = timeoutMs,
        MaxResponseSize = 16 * 1024 * 1024
    }.Request(url);

    private static int FindPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string WriteSettings(string source, string output, int port)
    {
        var config = JsonNode.Parse(File.ReadAllText(source))?.AsObject()
            ?? throw new ArgumentException("Settings file is empty.");
        var settings = config["Settings"]?.AsObject()
            ?? throw new ArgumentException("Settings file needs a Settings object.");
        foreach (string name in new[] { "CertificateFile", "KeyFile", "PublicRoot", "DataRoot" })
        {
            string value = settings[name]?.GetValue<string>()
                ?? throw new ArgumentException($"Settings is missing {name}.");
            string path = Path.GetFullPath(value, Path.GetDirectoryName(source)!);
            bool exists = name is "CertificateFile" or "KeyFile" ? File.Exists(path) : Directory.Exists(path);
            if (!exists) throw new ArgumentException($"{name} does not exist: {path}");
            settings[name] = name == "DataRoot" ? Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar : path;
        }
        settings["Host"] = "localhost";
        settings["Port"] = port;
        string destination = Path.Combine(output, "server-settings.json");
        File.WriteAllText(destination, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return destination;
    }

    private static string FindServerDll()
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "Server", "bin", configuration, "net8.0", "Kennedy.Server.dll");
            if (File.Exists(path)) return path;
        }
        throw new ArgumentException("Could not find the built server. Use --server <Kennedy.Server.dll>.");
    }

    private static Process StartServer(string dll, string settings, StreamWriter log)
    {
        if (!File.Exists(dll)) throw new ArgumentException($"Server binary not found: {dll}");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(settings);
        var process = new Process { StartInfo = start };
        void WriteLine(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) lock (log) log.WriteLine(e.Data);
        }
        process.OutputDataReceived += WriteLine;
        process.ErrorDataReceived += WriteLine;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void WaitForServer(Process server, Uri baseUrl, CancellationToken cancellation)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            cancellation.ThrowIfCancellationRequested();
            if (server.HasExited) throw new InvalidOperationException($"Server exited with code {server.ExitCode}; see server.log.");
            var response = Request(new Uri(baseUrl, "/search").AbsoluteUri, 1000);
            if (response.StatusCode == 10) return;
            Thread.Sleep(200);
        }
        throw new TimeoutException("Server did not become ready within 30 seconds; see server.log.");
    }

    private static void PrintHelp() => Console.WriteLine("""
        Kennedy server smoke test (no importing, indexing, or schema changes).

        dotnet run --project Kennedy.SmokeTests -- --settings <server.json>
        dotnet run --project Kennedy.SmokeTests -- --base-url gemini://localhost:1966/

        Options:
          --settings <file>       Start a local server using a copy of these settings.
          --server <dll>          Server binary; defaults to the matching solution build.
          --port <number>         Local high port; otherwise choose an available port.
          --base-url <url>        Check an already running capsule instead of starting one.
          --query <text>          Text and image search query (default: cat).
          --capsule <domain>      Known capsule with a working robots.txt (default: gemi.dev).
          --url <gemini-url>      Known URL for Page Info and URL checker (default: capsule root).
          --skip-network-tools   Skip robots, certificate, and URL checks that contact the target.
          --timeout <seconds>    Request timeout (default: 30).
          --output <directory>   Save responses, server log, and summary.json here.

        Exit codes: 0 = all selected checks passed; 1 = checks failed; 2 = setup failed; 130 = cancelled.
        """);

    private sealed record Check(string Name, string Path, string[] Markers, bool Results = false, bool Network = false);
    private sealed record CheckResult(string Name, string Url, bool Passed, int? Status, string? Meta, string? Error, long ElapsedMs);

    private sealed class Options
    {
        public string? Settings, Server, Output;
        public Uri? BaseUrl;
        public int? Port;
        public string Query = "cat", Capsule = "gemi.dev", Url = "";
        public int TimeoutSeconds = 30;
        public bool SkipNetworkTools;

        public static Options Parse(string[] args)
        {
            var result = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--skip-network-tools") { result.SkipNetworkTools = true; continue; }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                    throw new ArgumentException($"{arg} requires a value. Use --help for usage.");
                string value = args[++i];
                switch (arg)
                {
                    case "--settings": result.Settings = Path.GetFullPath(value); break;
                    case "--server": result.Server = Path.GetFullPath(value); break;
                    case "--output": result.Output = Path.GetFullPath(value); break;
                    case "--base-url": result.BaseUrl = new Uri(value, UriKind.Absolute); break;
                    case "--port": result.Port = int.Parse(value); break;
                    case "--query": result.Query = value; break;
                    case "--capsule": result.Capsule = value; break;
                    case "--url": result.Url = value; break;
                    case "--timeout": result.TimeoutSeconds = int.Parse(value); break;
                    default: throw new ArgumentException($"Unknown option: {arg}");
                }
            }
            if ((result.Settings == null) == (result.BaseUrl == null))
                throw new ArgumentException("Supply either --settings or --base-url. Use --help for usage.");
            if (result.BaseUrl != null && (result.BaseUrl.Scheme != "gemini" || result.BaseUrl.AbsolutePath != "/" ||
                result.BaseUrl.Query != "" || result.BaseUrl.Fragment != "" || result.BaseUrl.UserInfo != ""))
                throw new ArgumentException("--base-url must be a Gemini capsule root, such as gemini://localhost:1966/.");
            if (result.BaseUrl != null && (result.Port != null || result.Server != null))
                throw new ArgumentException("--port and --server apply only when starting a server with --settings.");
            if (result.Port is < 1024 or > 65535) throw new ArgumentException("--port must be between 1024 and 65535.");
            if (result.TimeoutSeconds is < 1 or > 300) throw new ArgumentException("--timeout must be between 1 and 300 seconds.");
            if (Uri.CheckHostName(result.Capsule) != UriHostNameType.Dns || !result.Capsule.Contains('.'))
                throw new ArgumentException("--capsule must be a domain such as gemi.dev, without a scheme, path, or port.");
            if (string.IsNullOrWhiteSpace(result.Query)) throw new ArgumentException("--query cannot be empty.");
            if (result.Url == "") result.Url = $"gemini://{result.Capsule}/";
            if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var url) || url.Scheme != "gemini" || url.Host == "")
                throw new ArgumentException("--url must be an absolute Gemini URL.");
            return result;
        }
    }
}
