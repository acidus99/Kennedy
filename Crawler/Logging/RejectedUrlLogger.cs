using Gemini.Net;

namespace Kennedy.Crawler.Logging;

public class RejectedUrlLogger
{
    StreamWriter fout;
    object locker;

    public RejectedUrlLogger(string outputFile)
    {
        locker = new object();
        fout = new StreamWriter(outputFile);
    }

    public void Close()
        => fout.Close();

    public void LogRejection(GeminiUrl url, string rejectionType, string specificRule = "")
    {
        lock (locker)
        {
            fout.WriteLine($"{rejectionType}\t{specificRule}\t{url}");
        }
    }
}
