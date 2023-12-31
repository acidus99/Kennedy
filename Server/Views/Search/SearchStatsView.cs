using Kennedy.SearchIndex;
using RocketForce;
using System.IO;
using System.Text.Json;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Shows the details about a 
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

        if (stats == null)
        {
            Response.WriteLine("Sorry, stats are unavailable right now. Please try again later.");
            return;
        }

        Response.WriteLine($"Active Capsules: {FormatCount(stats.Domains)}");
        Response.WriteLine($"Total Urls: {FormatCount(stats.Urls)}");
        Response.WriteLine($"Documents: {FormatCount(stats.SuccessUrls)}");
        Response.WriteLine($"Last Updated: {stats.LastUpdated}");

        return;
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
