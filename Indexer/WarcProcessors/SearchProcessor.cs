namespace Kennedy.Indexer.WarcProcessors;

using System;
using Warc;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex;
using Kennedy.Archive;
using System.Text.Json;

public class SearchProcessor : AbstractGeminiWarcProcessor
{
    SearchStorageWrapper wrapperDB;
    ResponseParser responseParser;
    string StorageDirectory;

    public SearchProcessor(string storageDirectory, string configDirectory)
        :base(configDirectory)
	{
        StorageDirectory = storageDirectory;

        if (!StorageDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            StorageDirectory += Path.DirectorySeparatorChar;
        }

        LanguageDetector.ConfigFileDirectory = configDirectory;

        wrapperDB = new SearchStorageWrapper(StorageDirectory);
        responseParser = new ResponseParser();
    }

    public override void FinalizeProcessing()
	{
        wrapperDB.FinalizeDatabases();
        WriteStatsFile();
    }

    protected override void ProcessGeminiResponse(GeminiResponse geminiResponse)
    {
        if(IsProactiveRequest(geminiResponse.RequestUrl))
        {
            return;
        }

        // Fully parsed the response to get type-specific metadata.
        ParsedResponse parsedResponse = responseParser.Parse(geminiResponse);
        wrapperDB.StoreResponse(parsedResponse);
    }

    /// <summary>
    /// Writes a statistics file to the archive output directory
    /// </summary>
    private void WriteStatsFile()
    {
        var stats = wrapperDB.GetSearchStats();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize<SearchStats>(stats, options);
        File.WriteAllText(StorageDirectory + "search-stats.json", json);
    }
}
