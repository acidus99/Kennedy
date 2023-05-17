namespace Kennedy.Indexer.WarcProcessors;

using System;
using Warc;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex;

public class SearchProcessor : AbstractGeminiWarcProcessor
{
    SearchStorageWrapper wrapperDB;
    ResponseParser responseParser;

    public SearchProcessor(string storageDirectory, string configDirectory)
	{
        LanguageDetector.ConfigFileDirectory = configDirectory;

        wrapperDB = new SearchStorageWrapper(storageDirectory);
        responseParser = new ResponseParser();
    }

    public override void FinalizeProcessing()
	{
        wrapperDB.FinalizeDatabases();
    }

    protected override void ProcessGeminiResponse(GeminiResponse geminiResponse)
    {
        // Fully parsed the response to get type-specific metadata.
        ParsedResponse parsedResponse = responseParser.Parse(geminiResponse);
        wrapperDB.StoreResponse(parsedResponse);
    }
}
