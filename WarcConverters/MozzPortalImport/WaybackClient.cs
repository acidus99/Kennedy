using System;
using System.Web;
using Newtonsoft.Json.Linq;


namespace Kennedy.WarcConverters.MozzPortalImport
{
	public class WaybackClient
	{
        // HttpClient is intended to be instantiated once per application, rather than per-use. See Remarks.
        static readonly HttpClient client = new HttpClient();

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

                    if (fields[4] != "200")
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
            }
            catch (Exception)
            {
            }

            return ret;
        }

        private static JObject ParseJson(string json)
           => JObject.Parse(json);

    }
}

