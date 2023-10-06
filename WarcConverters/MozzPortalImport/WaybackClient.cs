using System;
using System.Web;
using Newtonsoft.Json.Linq;


namespace Kennedy.WarcConverters.MozzPortalImport
{
	public class WaybackClient
	{
        // HttpClient is intended to be instantiated once per application, rather than per-use. See Remarks.
        static readonly HttpClient client = new HttpClient();

        public WaybackClient()
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("GeminiProxy/0.1 (gemini://gemi.dev/) gemini-proxy/0.1");
        }

        public List<string> GetUrls(string prefix)
		{
            List<String> ret = new List<string>();

			var apiUrl = $"https://web.archive.org/web/timemap/json?url={HttpUtility.UrlEncode(prefix)}&matchType=prefix&collapse=urlkey&output=json&fl=original%2Cmimetype%2Ctimestamp%2Cendtimestamp%2Cgroupcount%2Cuniqcount";

            string resp = client.GetStringAsync(apiUrl).GetAwaiter().GetResult();

            JArray json = JArray.Parse(resp);

            foreach(var tmp in json)
            {
                JArray entry = (tmp as JArray)!;

                var url = entry[0].Value<string>() ?? "";
                if(!url.StartsWith("http"))
                {
                    //skip header row
                    continue;
                }
                ret.Add(url);
            }
            return ret;
		}

        public List<WaybackSnapshot> GetSnapshots(string url)
        {
            List<WaybackSnapshot> ret = new List<WaybackSnapshot>();
            var apiUrl = $"https://web.archive.org/cdx/search/cdx?url={HttpUtility.UrlEncode(url)}";

            int remainingTries = 3;

            while (remainingTries > 0)
            {
                ret.Clear();
                try
                {
                    string resp = client.GetStringAsync(apiUrl).GetAwaiter().GetResult();
                    var lines = resp.Split('\n');
                    foreach (var line in lines)
                    {
                        var fields = line.Split(' ');
                        if (fields.Length != 7)
                        {
                            //skip lines that don't have the right number of fields
                            continue;
                        }

                        if (!System.Text.RegularExpressions.Regex.IsMatch(fields[4], @"[2-3]\d\d"))
                        {
                            //skip anything that isn't a 200
                            continue;
                        }

                        ret.Add(new WaybackSnapshot
                        {
                            Timestamp = fields[1],
                            OriginalUrl = fields[2],
                            ContentType = fields[3]
                        });
                    }
                    return ret;
                }
                catch (Exception)
                {
                }
                remainingTries--;
            }
            Console.WriteLine("FAILED AFTER 3 TRIES!!!!");
            return ret;
        }

        private static JObject ParseJson(string json)
           => JObject.Parse(json);

    }
}

