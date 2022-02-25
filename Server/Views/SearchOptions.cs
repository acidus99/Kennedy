using System;
using System.Net;
using Gemini.Net;
namespace Kennedy.Server.Views
{
    internal class SearchOptions
    {
        public int SearchPage { get; private set; } = 1;
        public int Algorithm { get; private set; } = 1;

        public SearchOptions(GeminiUrl url, string route)
        {
            var optionsPath = WebUtility.UrlDecode(url.Path).Substring(route.Length);
            if (optionsPath.Length > 0)
            {
                foreach (var option in optionsPath.Split('/'))
                {
                    if (option.Length == 0)
                    {
                        continue;
                    }
                    parseOption(option);
                }
            }
        }

        private void parseOption(string nv)
        {
            try
            {
                var parts = nv.Split(':');
                switch (parts[0].ToLower())
                {
                    case "p":
                        SearchPage = Convert.ToInt32(parts[1]);
                        break;
                    case "a":
                        Algorithm = Convert.ToInt32(parts[1]);
                        break;
                }
            }
            catch (Exception) { }
        }
    }
}
