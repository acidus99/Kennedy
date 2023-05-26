using System;
using System.Text.RegularExpressions;

using Gemini.Net;

namespace Kennedy.Crawler.Logging
{
    public static class HackLogger
    {
        static object locker = new object();

        public static void Log(GeminiUrl url, string direction)
        {
            var file = CrawlerOptions.Logs + "hack.log";
            string msg = $"{url.NormalizedUrl}\t@@@{direction}{Environment.NewLine}";

            lock (locker)
            {
                File.AppendAllText(file, msg);
            }
        }
    }
}

