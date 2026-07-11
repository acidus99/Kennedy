using Gemini.Net;

namespace Kennedy.Crawler.Logging;

public class RemainingUrlLogger
{
    StreamWriter fout;
    object locker;

    public RemainingUrlLogger(string outputFile)
    {
        locker = new object();
        var outputDir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        fout = new StreamWriter(outputFile)
        {
            AutoFlush = true
        };
    }

    public void Close()
        => fout.Close();

    public void LogRemainingUrl(UrlFrontierEntry entry)
    {
        lock (locker)
        {
            //TODO: Log more here
            fout.WriteLine($"{entry.Url}");
        }
    }
}
