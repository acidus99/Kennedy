using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.SearchIndex;
using System.Text.Json;
using Kennedy.Crawler.Filters;

namespace Kennedy.Indexer.WarcProcessors;

public class SearchProcessor : IGeminiRecordProcessor
{
    SearchStorageWrapper wrapperDB;
    ResponseParser responseParser;
    string StorageDirectory;

    public SearchProcessor(string storageDirectory, string configDirectory)
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

    public void FinalizeStores()
    {
        wrapperDB.FinalizeStores();
    }

    public void DoFinalGlobalWork()
    {
        wrapperDB.DoGlobalWork();
        WriteStatsFile();
    }

    public void ProcessGeminiResponse(GeminiResponse geminiResponse)
    {
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