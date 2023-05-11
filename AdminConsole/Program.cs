using System.Linq;

using Microsoft.EntityFrameworkCore;
using HashDepot;

using Gemini.Net;

using Kennedy.AdminConsole.WarcConverters;
using Kennedy.Warc;

using Kennedy.Archive;
using Kennedy.Data;
using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;
using Warc;

namespace Kennedy.AdminConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string workingDir = ResolveDir("~/HDD Inside/Kennedy-Work/");

            if(workingDir.Length == 0)
            {
                throw new ApplicationException("Set working directory!");
            }

            string warcOutputDir = workingDir + "WARCs/";
            string sourceDir = workingDir + "pre-WARC/";

            ImportFullDatabases(warcOutputDir, sourceDir + "crawldb - docs and domains/foo.txt");

            ImportPartialDatabase(warcOutputDir, sourceDir + "crawldb - docs only/2022-01-09/");

            ImportFullDatabasesNoPageStore(warcOutputDir,  sourceDir + "crawldb - bare db/foo.txt");

            ImportLegacyDatabases(warcOutputDir, sourceDir + "original-format/foo.txt");
        }

        /// <summary>
        /// Bulk imports Kennedy Search databases that have Documents, Domains, and are backed by a page-store
        /// </summary>
        /// <param name="warcOutputDir"></param>
        /// <param name="manifest"></param>
        static void ImportFullDatabases(string warcOutputDir, string manifest)
        {
            foreach (string crawlLocation in File.ReadLines(manifest))
            {
                var warcFile = CreateWarcName(crawlLocation);
                using (var warcCreator = new GeminiWarcCreator(warcOutputDir + warcFile))
                {
                    warcCreator.WriteWarcInfo(GetWarcFields());

                    AbstractConverter converter = new DomainConverter(warcCreator, crawlLocation);
                    converter.WriteToWarc();

                    converter = new DocumentConverter(warcCreator, crawlLocation);
                    converter.WriteToWarc();
                }
            }
        }

        /// <summary>
        /// Imports a Kennedy search database which has just a Documents table and is backed by a page-store
        /// </summary>
        /// <param name="warcOutputDir"></param>
        /// <param name="manifest"></param>
        static void ImportPartialDatabase(string warcOutputDir, string crawlLocation)
        {
            var warcFile = CreateWarcName(crawlLocation);
            using (var warcCreator = new GeminiWarcCreator(warcOutputDir + warcFile))
            {
                warcCreator.WriteWarcInfo(GetWarcFields());
                AbstractConverter converter= new DocumentConverter(warcCreator, crawlLocation);
                converter.WriteToWarc();
            }
        }

        /// <summary>
        /// Imports a Kennedy search database that doesn't have a backing page-store
        /// </summary>
        /// <param name="warcOutputDir"></param>
        /// <param name="manifest"></param>
        static void ImportFullDatabasesNoPageStore(string warcOutputDir, string manifest)
        {
            foreach (string crawlLocation in File.ReadLines(manifest))
            {
                var warcFile = CreateWarcName(crawlLocation);
                using (var warcCreator = new GeminiWarcCreator(warcOutputDir + warcFile))
                {
                    warcCreator.WriteWarcInfo(GetWarcFields());

                    //convert the domains
                    AbstractConverter converter = new DomainConverter(warcCreator, crawlLocation);
                    converter.WriteToWarc();
                    
                    converter = new BareConverter(warcCreator, crawlLocation);
                    converter.WriteToWarc();
                }
            }
        }


        /// <summary>
        /// Bulk imports original format Kennedy Search databases (log.tsv) and the directory-based page-store
        /// </summary>
        /// <param name="warcOutputDir"></param>
        /// <param name="manifest"></param>
        static void ImportLegacyDatabases(string warcOutputDir, string manifest)
        {
            foreach (string crawlLocation in File.ReadLines(manifest))
            {
                var warcFile = CreateWarcName(crawlLocation);
                using (var warcCreator = new GeminiWarcCreator(warcOutputDir + warcFile))
                {
                    warcCreator.WriteWarcInfo(GetWarcFields());

                    AbstractConverter converter = new LegacyConverter(warcCreator, crawlLocation);
                    converter.WriteToWarc();
                }
            }
        }

        static WarcFields GetWarcFields()
            => new WarcFields
                    {
                        {"software", "Kennedy Legacy Crawl importer"},
                        {"hostname", "kennedy.gemi.dev"},
                        {"operator", "Acidus"}
                    };

        static string CreateWarcName(string crawlLocation)
        {
            return Path.GetDirectoryName(crawlLocation)!.Split(Path.DirectorySeparatorChar).Reverse().First() + ".warc";
        }


        private static string ResolveDir(string dir)
            => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');

    }
}
