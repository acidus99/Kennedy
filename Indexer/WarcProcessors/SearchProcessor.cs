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
    ResponseParser responseParser;

    public SearchProcessor(string storageDirectory, string configDirectory)
	{
        LanguageDetector.ConfigFileDirectory = configDirectory;

        wrapperDB = new SearchStorageWrapper(storageDirectory);
        responseParser = new ResponseParser();
    }

    public void FinalizeProcessing()
	{
        wrapperDB.FinalizeDatabases();
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
            wrapperDB.StoreResponse(response);
        }
    }

    public ParsedResponse? ParseResponseRecord(ResponseRecord record)
    {
        GeminiUrl url = new GeminiUrl(record.TargetUri);

        try
        {
            var parsedResponse = responseParser.Parse(url, record.ContentBlock!);
            parsedResponse.RequestSent = record.Date;
            parsedResponse.ResponseReceived = record.Date;
            if (!string.IsNullOrEmpty(record.Truncated))
            {
                parsedResponse.IsBodyTruncated = true;
            }

            return parsedResponse;
        } catch(Exception ex)
        {
            return null;
        }
    }
}
