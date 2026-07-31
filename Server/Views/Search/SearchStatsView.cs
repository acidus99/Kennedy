using System.IO;
using System.Text.Json;
using Kennedy.SearchIndex;
using RocketForce;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Shows the details about the Kennedy software and search index
/// </summary>
internal class SearchStatsView : AbstractView
{

    public SearchStatsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();
        Response.WriteLine($"# 📏 Kennedy Stats");
        Response.WriteLine();

        var stats = GetStats();

        Response.WriteLine($"## Search Index");
        Response.WriteLine($"Database File Size: {FormatSearchIndexDatabaseSize()}");
        if (stats == null)
        {
            Response.WriteLine("Sorry, stats are unavailable right now. Please try again later.");
        }
        else
        {
            Response.WriteLine($"Active Capsules: {FormatCount(stats.Domains)}");
            Response.WriteLine($"Total Urls: {FormatCount(stats.Urls)}");
            Response.WriteLine($"Documents: {FormatCount(stats.SuccessUrls)}");
            Response.WriteLine($"Last Updated: {stats.LastUpdated}");
        }

        Response.WriteLine($"## Software info");
        Response.WriteLine($"Server version: {BuildInfo.Version}");

        return;
    }

    private string FormatSearchIndexDatabaseSize()
    {
        var databaseFile = new FileInfo(Settings.Global.SearchIndexDatabaseFile);

        if (!databaseFile.Exists)
        {
            return "Unavailable";
        }

        return FormatSize(databaseFile.Length);
    }

    private SearchStats? GetStats()
    {
        try
        {
            return JsonSerializer.Deserialize<SearchStats>(File.ReadAllText(Settings.Global.SearchStatsFile));
        }
        catch
        {
        }
        return null;
    }
}
