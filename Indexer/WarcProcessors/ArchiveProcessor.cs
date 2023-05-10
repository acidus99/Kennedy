namespace Kennedy.Indexer.WarcProcessors;

using System;
using System.Linq;
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
    Archiver archiver;
    Dictionary<Authority, bool> changedAuthorities = new Dictionary<Authority, bool>();

    public ArchiveProcessor(string archiveDirectory)
	{
        archiver = new Archiver(archiveDirectory + "archive.db", archiveDirectory + "Packs/");
    }

    public void FinalizeProcessing()
	{
        foreach(var authority in changedAuthorities.Keys)
        {
            var robots = GetRobots(authority);

            //no robots for domain that also contains archiver rules
            if(robots == null)
            {
                continue;
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
        if(robots.IsMalformed)
        {
            return null;
        }

        //we only care about Robots.txt files that have archiver rules.
        if (!robots.UserAgents.Contains("archiver"))
        {
            return null;
        }

        return robots;
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
