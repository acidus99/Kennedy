using System;
using System.Text;
using System.Text.RegularExpressions;

using System.Linq;
using System.Collections.Generic;
using GemiCrawler.DocumentIndex.Db;
using GemiCrawler.DocumentStore;
using GemiCrawler.Utils;
using GemiCrawler.GemText;
using Gemi.Net;
using System.IO;
using GemiCrawler.DocumentIndex;


namespace GemiCrawler.Support
{
    public static class TermDumper
    {
        public static void Dump(TermTracker tracker, int min, string outputDir)
        {

            var db = new DocIndexDbContext(Crawler.DataDirectory);


            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(outputDir + "tags/");

            var terms = tracker.GetSortedTerms(min);


            StreamWriter index = new StreamWriter(outputDir + "popular.gmi");
            foreach(var term in terms)
            {
                string outfile = $"tags/{term.Item1}.gmi";

                var vari = tracker.GetVariations(term.Item1);

                var variout = $"#{vari[0]}";
                if(vari.Count > 1)
                {
                    variout += $" (and #{vari[1]}";
                    for(int i =2; i < vari.Count; i++)
                    {
                        variout += $", #{vari[i]}";
                    }
                    variout += ")";
                }

                index.WriteLine($"=> {outfile} {variout} ({term.Item2})");


                StreamWriter fout = new StreamWriter(outputDir + outfile);
                fout.WriteLine($"# {term.Item1}");
                fout.WriteLine($"The hashtag #{term.Item1} appears on {term.Item2} pages in gemini space.");
                fout.WriteLine("Detected variants: " + variout);
                var urls = tracker.GetOccurences(term.Item1);
                urls.Sort();
                foreach (var url in urls)
                {
                    var dbID = DocIndex.toLong(url.DocID);

                    var title = (db.DocEntries.Where(x => x.DBDocID == dbID).Select(x => x.Title).FirstOrDefault());
                    title = title.Length > 0 ? title : $"{url.Hostname}{url.Path}";
                    fout.WriteLine($"=> {url.NormalizedUrl} {title}");
                }
                fout.Close();
            }
            index.Close();

            index = new StreamWriter(outputDir + "popularAZ.gmi");

            foreach (var term in terms.OrderBy(x => x.Item1))
            {
                string outfile = $"tags/{term.Item1}.gmi";

                var vari = tracker.GetVariations(term.Item1);

                var variout = $"#{vari[0]}";
                if (vari.Count > 1)
                {
                    variout += $" (and #{vari[1]}";
                    for (int i = 2; i < vari.Count; i++)
                    {
                        variout += $", #{vari[i]}";
                    }
                    variout += ")";
                }

                index.WriteLine($"=> {outfile} {variout} ({term.Item2})");
            }


            index.Close();
        }
    }
}
