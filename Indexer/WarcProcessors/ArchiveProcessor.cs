using System.Text.Json;
using Gemini.Net;
using Kennedy.Archive;
using Kennedy.Data.RobotsTxt;

namespace Kennedy.Indexer.WarcProcessors;

public class ArchiveProcessor : IGeminiRecordProcessor
{
    string ArchiveDirectory;
    Archiver archiver;
    Dictionary<Authority, bool> changedAuthorities = new Dictionary<Authority, bool>();

    public ArchiveProcessor(string archiveDirectory)
    {
        if (!archiveDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            archiveDirectory += Path.DirectorySeparatorChar;
        }

        archiver = new Archiver(archiveDirectory + "archive.db", archiveDirectory + "Packs/");
        ArchiveDirectory = archiveDirectory;
    }

    public void FinalizeStores()
    {
        //no finalizing of stores.
    }

    public void DoFinalGlobalWork()
    {
        UpdateVisbility();
        WriteStatsFile();
    }

    public void ProcessGeminiResponse(GeminiResponse geminiResponse)
    {
        //only if we successfully archived it should we track that
        //the domain should be checked against Robots.txt
        if (archiver.ArchiveResponse(geminiResponse))
        {
            var authority = new Authority
            {
                Domain = geminiResponse.RequestUrl.Hostname,
                Port = geminiResponse.RequestUrl.Port,
                Protocol = geminiResponse.RequestUrl.Protocol
            };

            if (!changedAuthorities.ContainsKey(authority))
            {
                changedAuthorities.Add(authority, true);
            }
        }
    }

    /// <summary>
    /// Updates the visibility of all contents in the archive using directives
    /// from the latest copy of robots.txt that we have
    /// </summary>
    private void UpdateVisbility()
    {
        foreach (var authority in changedAuthorities.Keys)
        {
            UpdateVisibilityForDomain(authority);
        }
    }

    /// <summary>
    /// Toggles whether URLs in a domain are visible in the archive or not
    /// bassed on the latest version of Robots.txt
    /// </summary>
    /// <param name="authority"></param>
    private void UpdateVisibilityForDomain(Authority authority)
    {
        var robots = GetRobots(authority);

        //no robots for domain that also contains archiver rules
        if (robots == null)
        {
            return;
        }

        using (var db = archiver.GetContext())
        {
            var urls = db.Urls.Where(x => x.Protocol == authority.Protocol &&
                                x.Domain == authority.Domain &&
                                x.Port == authority.Port);

            foreach (var url in urls)
            {
                bool allowed = robots.IsPathAllowed("archiver", url.GeminiUrl.Path);

                if (!allowed && url.IsPublic)
                {
                    url.IsPublic = false;
                    db.SaveChanges();
                }
                else if (allowed && !url.IsPublic)
                {
                    url.IsPublic = true;
                    db.SaveChanges();
                }
            }
        }
    }

    /// <summary>
    /// Writes a statistics file to the archive output directory
    /// </summary>
    private void WriteStatsFile()
    {
        var stats = archiver.GetArchiveStats();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize<ArchiveStats>(stats, options);
        File.WriteAllText(ArchiveDirectory + "archive-stats.json", json);
    }

    private RobotsTxtFile? GetRobots(Authority authority)
    {
        var robotsUrl = new GeminiUrl(RobotsTxtParser.CreateRobotsUrl(authority.Protocol, authority.Domain, authority.Port));

        GeminiResponse? geminiResponse = archiver.GetLatestResponse(robotsUrl.ID);

        if (geminiResponse == null)
        {
            return null;
        }
        if (!geminiResponse.IsSuccess || !geminiResponse.HasBody)
        {
            return null;
        }

        RobotsTxtParser robotsTxtParser = new RobotsTxtParser();
        RobotsTxtFile robots = robotsTxtParser.Parse(geminiResponse.BodyText);

        //we want all valid robots, since a later robots might have disallow rules against
        //all user agents, not just archiver, and we don't want to miss those
        return robots.HasValidRules ?
            robots :
            null;
    }

    private struct Authority
    {
        public string Domain { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
    }
}
