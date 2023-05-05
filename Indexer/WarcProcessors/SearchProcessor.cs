namespace Kennedy.Indexer.WarcProcessors;

using System;
using Warc;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex;


public class SearchProcessor : IWarcProcessor
{

    SearchStorageWrapper wrapperDB;
    Dictionary<string, ServerInfo> servers;
    ResponseParser responseParser;

    public SearchProcessor(string storageDirectory, string configDirectory)
	{
        LanguageDetector.ConfigFileDirectory = configDirectory;

        wrapperDB = new SearchStorageWrapper(storageDirectory);
        servers = new Dictionary<string, ServerInfo>();
        responseParser = new ResponseParser();
    }

    public void FinalizeProcessing()
	{
        wrapperDB.FinalizeDatabases();

        foreach (ServerInfo domainInfo in servers.Values)
        {
            wrapperDB.StoreDomain(domainInfo);
        }

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

        wrapperDB.StoreResponse(response);
        HandleServer(response);
    }

    private ParsedResponse ParseResponseRecord(ResponseRecord record)
    {
        GeminiUrl url = new GeminiUrl(record.TargetUri);
        var parsedResponse = responseParser.Parse(url, record.ContentBlock!);
        parsedResponse.RequestSent = record.Date;
        parsedResponse.ResponseReceived = record.Date;
        if (!string.IsNullOrEmpty(record.Truncated))
        {
            parsedResponse.IsBodyTruncated = true;
        }

        return parsedResponse;
    }

    private void HandleServer(ParsedResponse response)
    {
        string key = "gemini:" + response.RequestUrl.Authority;

        var received = response.ResponseReceived;
        bool isReachable = !response.IsConnectionError;

        if (!servers.ContainsKey(key))
        {
            servers.Add(key, new ServerInfo
            {
                Domain = response.RequestUrl.Hostname,
                Port = response.RequestUrl.Port,
                Protocol = response.RequestUrl.Protocol,
                IsReachable = isReachable,
                ErrorMessage = (isReachable) ? null : response.Meta,
                FirstSeen = received,
                LastVisit = received,
                LastSuccessfulVisit = (isReachable) ? received : null
            });
        }

        var server = servers[key];

        //is this an earlier visit than I have?
        if(received > server.FirstSeen)
        {
            server.FirstSeen = received;
        }
        if(received > server.LastVisit)
        {
            server.LastVisit = received;
        }
        if (received > server.LastSuccessfulVisit && isReachable)
        {
            server.LastSuccessfulVisit = received;
        }

        if (response.IsSuccess)
        {
            if (response.RequestUrl.Path == "/robots.txt")
            {
                servers[key].RobotsUrlID = response.RequestUrl.ID;
            }
            else if (response.RequestUrl.Path == "/favicon.txt" && IsValidFavicon(response.BodyText))
            {
                servers[key].FaviconUrlID = response.RequestUrl.ID;
                servers[key].FaviconTxt = response.BodyText;
            }
            else if (response.RequestUrl.Path == "/.well-known/security.txt" && IsValidSecurity(response.BodyText))
            {
                servers[key].SecurityUrlID = response.RequestUrl.ID;
            }
        }
    }

    private bool IsValidFavicon(string contents)
        => (contents != null && !contents.Contains(" ") && !contents.Contains("\n") && contents.Length < 20);

    private bool IsValidSecurity(string contents)
        => (contents != null && contents.ToLower().Contains("contact:"));

}
