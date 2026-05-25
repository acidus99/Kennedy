using System.Linq;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class SearchStatsView : AbstractView
{
    public SearchStatsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();
        Response.WriteLine("# 📏 Kennedy Stats");
        Response.WriteLine();

        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;

        using var db = new KennedyDbContext(options);

        var domains = db.UrlRegistry
            .Where(x => x.LastStatusCode != Gemini.Net.GeminiParser.ConnectionErrorStatusCode)
            .Select(x => new { x.Scheme, x.Host, x.Port })
            .Distinct()
            .Count();

        var totalUrls = db.UrlRegistry.LongCount();
        var documents = db.Documents.LongCount();
        var lastUpdated = db.UrlRegistry
            .Where(x => x.LastVisit != null)
            .Select(x => x.LastVisit)
            .Max();

        Response.WriteLine($"Active Capsules: {FormatCount(domains)}");
        Response.WriteLine($"Total Urls: {FormatCount(totalUrls)}");
        Response.WriteLine($"Documents: {FormatCount(documents)}");
        Response.WriteLine($"Last Updated: {lastUpdated}");
    }
}
