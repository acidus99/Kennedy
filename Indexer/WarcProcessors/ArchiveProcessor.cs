namespace Kennedy.Indexer.WarcProcessors;

using System;
using Warc;

using Gemini.Net;

using Kennedy.Archive;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex.Web;


public class ArchiveProcessor : IWarcProcessor
{
    Archiver archiver;

    public ArchiveProcessor(string archiveDirectory, string configDirectory)
	{
        archiver = new Archiver(archiveDirectory + "archive.db", archiveDirectory + "Packs/");
    }

    public void FinalizeProcessing()
	{

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
            archiver.ArchiveResponse(response);
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

}
