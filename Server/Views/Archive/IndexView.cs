using System;
using System.IO;
using System.Text.Json;

using System.Collections.Generic;
using Kennedy.Archive;
using RocketForce;

namespace Kennedy.Server.Views.Archive
{

    /// <summary>
    /// Shows the details about a 
    /// </summary>
    internal class IndexView :AbstractView
    {

        public IndexView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            Response.Success();
            Response.WriteLine($"# 🏎 Delorean Time Machine");
            Response.WriteLine();
            Response.WriteLine("Welcome to Delorean, the Time Machine for Geminispace!");
            var stats = GetStats();
            Response.WriteLine();
            if (stats != null)
            {
                Response.WriteLine($"🗄️ Storing {FormatCount(stats.Captures)} captures of {FormatCount(stats.UrlsTotal)} different URLs, across {FormatCount(stats.Domains)}!");
            }
            Response.WriteLine(@"
=> /archive/search Search for URLs in the Time Machine (e.g. ""mozz.us"" or ""/starwars/"")
=> /archive/history View captures for a specific URL

=> faq.gmi Delorean FAQ
=> about.gmi About the Time Machine
=> submit.gmi I want your old content!
=> stats 📏 Archive Statistics
");
        }

        private ArchiveStats? GetStats()
        {
            try
            {
                return JsonSerializer.Deserialize<ArchiveStats>(File.ReadAllText(Settings.Global.ArchiveStatsFile));
            }
            catch
            {

            }
            return null;
        }
    }
}
