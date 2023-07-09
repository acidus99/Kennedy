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
    internal class ArchiveStatsView :AbstractView
    {

        public ArchiveStatsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            Response.Success();
            Response.WriteLine($"# 📏 Archive Statistics");
            Response.WriteLine();

            var stats = GetStats();

            if(stats == null)
            {
                Response.WriteLine("Sorry, stats are unavailable right now. Please try again later.");
                return;
            }

            Response.WriteLine("## Contents");
            Response.WriteLine($"Urls: {FormatCount(stats.UrlsTotal)}");
            Response.WriteLine($"Captures: {FormatCount(stats.Captures)}");
            Response.WriteLine($"Unique Captures: {FormatCount(stats.CapturesUnique)}");
            Response.WriteLine($"Capsules: {FormatCount(stats.Domains)}");

            Response.WriteLine("## Time Coverage");
            Response.WriteLine($"Oldest Capture: {stats.OldestSnapshot}");
            Response.WriteLine($"Latest Capture: {stats.NewestSnapshot}");

            Response.WriteLine("## Archive Size");
            Response.WriteLine($"Uncompressed size (All content): {FormatSize(stats.SizeWithoutDeDuplication)}");
            Response.WriteLine($"Uncompressed size (Deduplicated): {FormatSize(stats.Size)}");
            Response.WriteLine($"Savings from Deduplication: {FormatSavings(stats.Size, stats.SizeWithoutDeDuplication)}");
            Response.WriteLine($"Actual size on disk (captures deduplicated, compressed where possible): {FormatSize(EstimatedSizeOnDisk(stats.Size))}");
                
            return;
        }

        //current average savings of Pack files is ~22%
        private long EstimatedSizeOnDisk(long size)
            => Convert.ToInt64(Math.Truncate(Convert.ToDouble(size) * (1d - 0.095d)));

        private string FormatSavings(long optimized, long unoptimized)
        {
            double savings = (1d - (Convert.ToDouble(optimized) / Convert.ToDouble(unoptimized)));
            return string.Format("{0:P}", savings);
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
