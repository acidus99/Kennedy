using System.IO;
using Gemini.Net;

namespace Kennedy.Crawler.Logging
{
    public class ResponseLogger
    {
        StreamWriter fout;
        object locker;

        public ResponseLogger(string outputFile)
        {
            locker = new object();
            fout = new StreamWriter(outputFile);
        }

        public void Close()
            => fout.Close();

        public void LogUrlResponse(GeminiResponse response)
        {
            lock (locker)
            {
                fout.WriteLine($"{response.StatusCode}\t\"{Clean(response.Meta)}\t{response.RequestUrl}");
            }
        }

        /// <summary>
        /// cleans a string to be a field in a CSV
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string Clean(string s)
            => s.Replace("\n", "").Replace("\r", "").Replace(",", "-");
    }
}
