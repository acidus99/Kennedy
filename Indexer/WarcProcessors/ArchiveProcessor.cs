namespace Kennedy.Indexer.WarcProcessors;

using System;
using System.Linq;
using System.Text.Json;
using Warc;

using Gemini.Net;

using Kennedy.Archive;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex.Web;
using Kennedy.Data.RobotsTxt;
using Kennedy.Archive.Db;


public class ArchiveProcessor : IWarcProcessor
{
    string ArchiveDirectory;
    Archiver archiver;
    Dictionary<Authority, bool> changedAuthorities = new Dictionary<Authority, bool>();

    public ArchiveProcessor(string archiveDirectory)
	{
        archiver = new Archiver(archiveDirectory + "archive.db", archiveDirectory + "Packs/");
        ArchiveDirectory = archiveDirectory;
    }

    public void FinalizeProcessing()
	{
        UpdateVisbility();
        WriteStatsFile();
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
        File.WriteAllText(ArchiveDirectory + "stats.json", json);
    }

    private RobotsTxtFile? GetRobots(Authority authority)
    {
        var robotsUrl = new GeminiUrl(RobotsTxtFile.CreateRobotsUrl(authority.Protocol, authority.Domain, authority.Port));

        GeminiResponse? geminiResponse = archiver.GetLatestResponse(robotsUrl.ID);

        if(geminiResponse == null)
        {
            return null;
        }
        if(!geminiResponse.IsSuccess || !geminiResponse.HasBody)
        {
            return null;
        }

        RobotsTxtFile robots = new RobotsTxtFile(geminiResponse.BodyText);

        //we want all valid robots, since a later robots might have disallow rules against
        //all user agents, not just archiver, and we don't want to miss those
        return !robots.IsMalformed ?
            robots :
            null;
    }

    public void ProcessRecord(WarcRecord record)
	{
        if(record.Type == "response")
        {
            ProcessResponseRecord((ResponseRecord) record);
        }
	}

    private void ProcessResponseRecord(ResponseRecord responseRecord)
    {
        var response = ParseResponseRecord(responseRecord);
        if (response != null)
        {
            //only if we successfully archived it should we track that
            //the domain should be checked against Robots.txt
            if(archiver.ArchiveResponse(response))
            {
                var authority = new Authority
                {
                    Domain = response.RequestUrl.Hostname,
                    Port = response.RequestUrl.Port,
                    Protocol = response.RequestUrl.Protocol
                };

                if (!changedAuthorities.ContainsKey(authority))
                {
                    changedAuthorities.Add(authority, true);
                }
            }
        }
    }

    private GeminiResponse? ParseResponseRecord(ResponseRecord record)
    {
        GeminiUrl url = new GeminiUrl(StripRobots(record.TargetUri));

        try
        {
            var response = GeminiParser.ParseResponseBytes(url, record.ContentBlock);
            response.RequestSent = record.Date;
            response.ResponseReceived = record.Date;
            response.IsBodyTruncated = (record.Truncated?.Length > 0);
            return response;
        } catch(Exception ex)
        {
            return null;
        }
    }

    private Uri StripRobots(Uri url)
    {
        if(url.PathAndQuery == "/robots.txt?kennedy-crawler")
        {
            UriBuilder uriBuilder = new UriBuilder(url);
            uriBuilder.Query = "";
            return uriBuilder.Uri;
        }
        return url;
    }

    private struct Authority
    {
        public string Domain { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
    }
}
