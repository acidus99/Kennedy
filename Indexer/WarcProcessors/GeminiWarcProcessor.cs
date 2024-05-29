using Gemini.Net;
using Kennedy.Crawler.Filters;
using Kennedy.Data.Utils;
using WarcDotNet;

namespace Kennedy.Indexer.WarcProcessors;

public class GeminiWarcProcessor
{
    public List<IGeminiRecordProcessor> RecordProcessors;

    BlockListFilter denyFilter;

    public GeminiWarcProcessor(string configDirectory)
    {
        denyFilter = new BlockListFilter(configDirectory);
        RecordProcessors = new List<IGeminiRecordProcessor>();
    }

    public void ProcessRecord(WarcRecord record)
    {
        if (record.Type == RecordType.Response)
        {
            var geminiResponse = GetGeminiResponse((record as ResponseRecord)!);
            if (geminiResponse != null)
            {
                foreach(IGeminiRecordProcessor recordProcessor in RecordProcessors)
                {
                    recordProcessor.ProcessGeminiResponse(geminiResponse);
                }
            }
        }
    }

    /// <summary>
    /// Final things to do for a specific WARC
    /// </summary>
    public void CompleteWarcProcessing()
    {
        foreach (IGeminiRecordProcessor recordProcessor in RecordProcessors)
        {
            recordProcessor.FinalizeStores();
        }
    }

    public void FinalizeAllProcessing()
    {
        foreach (IGeminiRecordProcessor recordProcessor in RecordProcessors)
        {
            recordProcessor.DoFinalGlobalWork();
        }
    }

    private GeminiResponse? GetGeminiResponse(ResponseRecord responseRecord)
    {
        if (responseRecord.TargetUri == null || responseRecord.ContentBlock == null || responseRecord.TargetUri.Scheme != "gemini")
        {
            return null;
        }

        try
        {
            bool isProactive = UrlUtility.IsProactiveUrl(responseRecord.TargetUri);
            var url = new GeminiUrl(UrlUtility.RemoveCrawlerIdentifier(responseRecord.TargetUri));
            if (!isProactive)
            {
                BlockResult result = denyFilter.IsUrlAllowed(url);
                if (!result.IsAllowed)
                {
                    //File.AppendAllText("/tmp/deny.txt", result.Reason + "\n");
                    return null;
                }
            }
            var response = GeminiParser.ParseResponseBytes(url, responseRecord.ContentBlock);
            response.RequestSent = responseRecord.Date;
            response.ResponseReceived = responseRecord.Date;
            response.IsBodyTruncated = (responseRecord.Truncated?.Length > 0);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Malformed Gemini response record: {ex.Message}");
            return null;
        }
    }
}