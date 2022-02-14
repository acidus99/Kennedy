using System;

using Gemini.Net;

namespace Kennedy.Crawler
{
    public class DomainAnalyzer
    {
        public string Host { get; private set; }
        public int Port { get; private set; }

        public bool IsReachable { get; private set; }
        public string ErrorMessage { get; private set; }

        public string RobotsTxt { get; private set; }
        public string SecurityTxt { get; private set; }
        public string FaviconTxt { get; private set; }

        public bool HasValidRobotsTxt => !String.IsNullOrEmpty(RobotsTxt);
        public bool HasValidFavionTxt => !String.IsNullOrEmpty(FaviconTxt);
        public bool HasValidSecurityTxt => !String.IsNullOrEmpty(SecurityTxt);

        public DomainAnalyzer(string host, int port)
        {
            Host = host;
            Port = port;
            IsReachable = false;
        }

        public void QueryDomain(DnsCache dnsCache)
        {
            if (dnsCache.GetLookup(Host) != null)
            {
                IsReachable = true;
                CheckRobots();
                CheckFavicon();
                CheckSecurity();
            }            
        }

        private void CheckRobots()
        {
            var txt = GetTextForFile("/robots.txt");
            if (txt.ToLower().Contains("user-agent:"))
            {
                RobotsTxt = txt;
            }
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
            SecurityTxt = GetTextForFile("/.well-known/security.txt");
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
                return resp.BodyText.Trim();
            }
            return "";
        }

    }
}
