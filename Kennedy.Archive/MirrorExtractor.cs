using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Gemini.Net;

using Kennedy.CrawlData.Db;
using Kennedy.CrawlData;

namespace Kennedy.Archive
{

    /// <summary>
    /// Extracts a capsule from the data storage and puts it in a output
    /// </summary>
    public class MirrorExtractor
    {
        string DocDBLocation;
        string OutputLocation;
        string UrlPathRoot;

        public MirrorExtractor(string dbLocation, string outputLocation)
        {
            DocDBLocation = EnsureSlash(dbLocation);
            OutputLocation = EnsureSlash(outputLocation);
        }

        public void CreateMirror(string domainToClone, int portToClone, string urlPathRoot)
        {
            UrlPathRoot = EnsureSlash(urlPathRoot);

            // /Users/billy/tmp/DD/   
            DocIndexDbContext db = new DocIndexDbContext(DocDBLocation);
            DocumentStore docStore = new DocumentStore($"{DocDBLocation}page-store/");

            foreach (var entry in db.DocEntries.Where(x => x.Domain == domainToClone && x.Port == portToClone && x.Status == 20))
            {
                
                entry.SetDocID();
                byte[] data = null;
                try
                {
                    data = docStore.GetDocument(entry.DocID);
                }
                catch (Exception)
                {

                }
                if (data != null)
                {

                    Uri originalUrl = new Uri(entry.Url);

                    //something to output.
                    Uri newUrl = RewriteUrl(originalUrl);

                    string outputFile = OutputFileForUrl(newUrl);

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

        public string RewriteGemtext(Uri originalUrl, string gemtext)
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
                    sb.Append(RewriteLine(originalUrl, line));
                    sb.Append('\n');

                }
            }
            return sb.ToString();
        }

        //Creates the output filename for a url. Handles index.gmi
        private string OutputFileForUrl(Uri newUrl)
        {
            var path = Path.Combine(OutputLocation , newUrl.AbsolutePath.Substring(1));
            if(path.EndsWith('/'))
            {
                path += "index.gmi";
            }
            return path;
        }

        private string RewriteLine(Uri originalUrl, string line)
        {
            var match = linkLine.Match(line);
            if(!match.Success)
            {
                //not a link line, pass it through
                return line;
            }

            Uri linkUrl = CreateUri(originalUrl, match);

            if(!ShouldRewrite(linkUrl, originalUrl))
            {
                //not rewritting, pass it through
                return line;
            }

            string linkText = getLinkText(match);
            Uri newUrl = RewriteUrl(linkUrl);
            if (linkText.Length > 0)
            {
                return $"=> {newUrl.AbsolutePath} {linkText}";
            }
            else
            {
                return $"=> {newUrl.AbsolutePath}";
            }
        }

        private string EnsureSlash(string s)
            => s.EndsWith('/') ? s : s + '/';

        private Uri RewriteUrl(Uri url)
        {
            UriBuilder b = new UriBuilder(url);

            if (b.Path.StartsWith("/") && b.Path.Length >= 2)
            {
                b.Path =UrlPathRoot + b.Path.Substring(1);
            } else
            {
                b.Path = UrlPathRoot;
            }
            return b.Uri;
        }

        private Uri CreateUri(Uri originalUrl, Match match)
            => new Uri(originalUrl, match.Groups[1].Value);

        private bool ShouldRewrite(Uri url, Uri originalUrl)
        {
            if(url.Scheme != "gemini" || url.Host != originalUrl.Host)
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
