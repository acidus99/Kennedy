using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Logging;

public class RemainingUrlLogger
{
    StreamWriter fout;
    object locker;

    public RemainingUrlLogger(string outputFile)
    {
        locker = new object();
        fout = new StreamWriter(outputFile);
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