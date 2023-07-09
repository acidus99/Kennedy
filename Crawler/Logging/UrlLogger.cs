using System.IO;

namespace Kennedy.Crawler.Logging
{
    public class UrlLogger
    {
        StreamWriter fout;
        object locker;

        public UrlLogger(string outputFile)
        {
            locker = new object();
            fout = new StreamWriter(outputFile);
        }

        public void Close()
            => fout.Close();

        public void Log(string msg, string url)
        {
            lock (locker)
            {
                fout.WriteLine($"{msg}\t{url}");
            }
        }
    }
}
