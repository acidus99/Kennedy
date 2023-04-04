using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Gemini.Net;

using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex;

namespace Kennedy.Archive
{

    /// <summary>
    /// Extracts a capsule from the data storage and puts it in a output
    /// </summary>
    public class MirrorExtractor
    {
        string DocDBLocation;
        string BaseOutputLocation;
        string OutputLocation;

        public MirrorExtractor(string dbLocation, string outputLocation)
        {
            DocDBLocation = EnsureSlash(dbLocation);
            BaseOutputLocation = EnsureSlash(outputLocation);
        }

        public void CreateMirror(string domainToClone, int portToClone)
        {
            OutputLocation = EnsureSlash(BaseOutputLocation + domainToClone);
            
            SearchIndexContext db = new SearchIndexContext(DocDBLocation);
            DocumentStore docStore = new DocumentStore($"{DocDBLocation}page-store/");

            foreach (var entry in db.Documents
                .Where(x => x.Domain == domainToClone && x.Port == portToClone && x.Status == 20))
            {
                byte[] data = null;
                try
                {
                    data = docStore.GetDocument(entry.UrlID);
                }
                catch (Exception)
                {

                }
                if (data != null)
                {

                    Uri originalUrl = new Uri(entry.Url);

                    string outputFile = OutputFileForUrl(originalUrl);

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    if (entry.MimeType.StartsWith("text/gemini"))
                    {
                        //get data here, use charset in the future
                        string content = Encoding.UTF8.GetString(data);
                        content = RewriteGemtext(originalUrl, content);
                        File.WriteAllText(outputFile, content);
                    }
                    else
                    {
                        //write it out to disk
                        File.WriteAllBytes(outputFile, data);
                    }
                }
            }

        }

        public string RewriteGemtext(Uri sourceUrl, string gemtext)
        {
            bool inPre = false;
            StringBuilder sb = new StringBuilder();

            //not sure how to make this linq since I'm flip/flopping state
            foreach (var line in gemtext.Split("\n"))
            {
                if (line.StartsWith("```"))
                {
                    inPre = !inPre;
                    sb.Append(line);
                    sb.Append('\n');
                    continue;
                }
                if (inPre)
                {
                    sb.Append(line);
                    sb.Append('\n');
                }
                else
                {
                    sb.Append(RewriteLine(sourceUrl, line));
                    sb.Append('\n');

                }
            }
            return sb.ToString();
        }

        //Creates the output filename for a url. Handles index.gmi
        private string OutputFileForUrl(Uri newUrl)
        {
            var path = AddDefaultFile(newUrl.AbsolutePath);
            //skip the leading /
            return Path.Combine(OutputLocation, path.Substring(1));
        }

        private string RewriteLine(Uri sourceUrl, string line)
        {
            var match = linkLine.Match(line);
            if (!match.Success)
            {
                //not a link line, pass it through
                return line;
            }

            Uri targetUrl = CreateUri(sourceUrl, match);

            if (!ShouldRewrite(targetUrl, sourceUrl))
            {
                //not rewritting, pass it through
                return line;
            }

            string linkText = getLinkText(match);
            string relativeUrl = ToRelativeUrl(sourceUrl, targetUrl);
            if (linkText.Length > 0)
            {
                return $"=> {relativeUrl} {linkText}";
            }
            else
            {
                return $"=> {relativeUrl}";
            }
        }

        private string EnsureSlash(string s)
            => s.EndsWith('/') ? s : s + '/';

        private string AddDefaultFile(string path)
            => path.EndsWith('/') ? path + "index.gmi" : path;

        private string ToRelativeUrl(Uri sourceUrl, Uri targetUrl)
        {
            string target = AddDefaultFile(targetUrl.AbsolutePath);
            return Path.GetRelativePath(Path.GetDirectoryName(sourceUrl.AbsolutePath) ?? "/", target);
        }

        private Uri CreateUri(Uri originalUrl, Match match)
            => new Uri(originalUrl, match.Groups[1].Value);

        private bool ShouldRewrite(Uri url, Uri originalUrl)
        {
            if (url.Scheme != "gemini" || url.Host != originalUrl.Host)
            {
                return false;
            }

            GeminiUrl gNew = new GeminiUrl(url);
            GeminiUrl gOrig = new GeminiUrl(originalUrl);
            return (gNew.Port == gOrig.Port);
        }

        /// <summary>
        /// gives us the text, if any, used with this link
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private static string getLinkText(Match match)
            => (match.Groups.Count > 2) ? match.Groups[2].Value : "";

        static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

    }
}
