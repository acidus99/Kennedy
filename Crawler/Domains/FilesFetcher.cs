using System;

using Gemini.Net;
using Kennedy.Crawler.Crawling;
using Kennedy.Crawler.RobotsTxt;

namespace Kennedy.Crawler.Domains
{
    public class FilesFetcher
    {
        public string Host { get; private set; }
        public int Port { get; private set; }

        public bool IsReachable { get; private set; }

        public string RobotsTxt { get; private set; }
        public string SecurityTxt { get; private set; }
        public string FaviconTxt { get; private set; }

        public bool HasValidRobotsTxt => !String.IsNullOrEmpty(RobotsTxt);
        public bool HasValidFavionTxt => !String.IsNullOrEmpty(FaviconTxt);
        public bool HasValidSecurityTxt => !String.IsNullOrEmpty(SecurityTxt);

        IWebCrawler Crawler;

        public FilesFetcher(string host, int port, IWebCrawler crawler)
        {
            Host = host;
            Port = port;
            IsReachable = false;
            Crawler = crawler;
        }

        public void FetchFiles(bool isReachable)
        {
            if (isReachable)
            {
                IsReachable = true;
                CheckRobots();
                CheckFavicon();
                CheckSecurity();
            }
        }

        private void CheckRobots()
        {
            //we can pull it from the global Robots cache, since that gets populated
            //for a domain before any requests to that domain are made, which happens
            //before this is called
            RobotsTxt = RobotsChecker.Global.GetFromCache(Host, Port)?.Contents ?? "";
        }

        private void CheckFavicon()
        {
            var favicon = GetTextForFile("/favicon.txt");
            if (!favicon.Contains(" ") && !favicon.Contains("\n") && favicon.Length < 20)
            {
                FaviconTxt = favicon;
            }
        }

        private void CheckSecurity()
        {
            var txt = GetTextForFile("/.well-known/security.txt");
            if (txt.ToLower().Contains("contact:"))
            {
                SecurityTxt = txt;
            }
        }

        private GeminiResponse GetFile(string path)
        {
            var url = new GeminiUrl($"gemini://{Host}:{Port}{path}");
            var requestor = new GeminiRequestor();

            return  requestor.Request(url);
        }

        private string GetTextForFile(string path)
        {
            var resp = GetFile(path);
            if(resp.IsSuccess && resp.HasBody && resp.IsTextResponse)
            {
                //beacon it out
                Crawler.ProcessRequestResponse(resp, null);
                return resp.BodyText.Trim();
            }
            return "";
        }

    }
}
