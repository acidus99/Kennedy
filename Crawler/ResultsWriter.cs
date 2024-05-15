using System.Collections.Concurrent;
using Gemini.Net;
using Kennedy.Warc;
using WarcDotNet;

namespace Kennedy.Crawler;

/// <summary>
/// Holds responses and flushes them to a WARC file
/// </summary>
public class ResultsWriter
{
    const int MaxUninterestingFileSize = 10 * 1024;

    ConcurrentQueue<GeminiResponse> responses;
    GeminiWarcCreator warcCreator;

    public int Saved { get; private set; }

    public ResultsWriter(string warcDirectory)
    {
        Saved = 0;
        responses = new ConcurrentQueue<GeminiResponse>();
        warcCreator = new GeminiWarcCreator(warcDirectory + DateTime.Now.ToString("yyyy-MM-dd") + ".warc.gz");
        warcCreator.WriteWarcInfo(new WarcInfoFields
        {
            {"software", "Kennedy Crawler"},
            {"hostname", "kennedy.gemi.dev"},
            {"timestamp", DateTime.Now },
            {"operator", "Acidus"}
        });
    }

    public void AddToQueue(GeminiResponse response)
        => responses.Enqueue(response);

    public void Flush()
    {
        GeminiResponse? response;
        while (responses.TryDequeue(out response))
        {
            WriteResponseToWarc(response);
        }
    }

    public void Close()
    {
        Flush();
        warcCreator.Dispose();
    }

    private void WriteResponseToWarc(GeminiResponse response)
    {
        GeminiResponse optimizedResponse = OptimizeForStoage(response);
        warcCreator.WriteSession(optimizedResponse);
        Saved++;
    }

    private GeminiResponse OptimizeForStoage(GeminiResponse response)
    {
        if (!response.HasBody || response.MimeType == null)
        {
            return response;
        }

        if (response.MimeType.StartsWith("text/") || response.MimeType.StartsWith("image/"))
        {
            return response;
        }

        if (response.BodySize > MaxUninterestingFileSize)
        {
            response.BodyBytes = response.BodyBytes!.Take(MaxUninterestingFileSize).ToArray();
            response.IsBodyTruncated = true;
        }

        return response;
    }

}