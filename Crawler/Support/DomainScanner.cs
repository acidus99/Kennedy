using System;
using System.Linq;
using System.IO;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Gemini.Net;
using Kennedy.Crawler.GemText;
using System.Text;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.Support
{
    public static class DomainScanner
    {
        public static void DoIt()
        {

            ThreadSafeCounter counter = new ThreadSafeCounter();

            var db = new DocIndexDbContext(CrawlerOptions.DataDirectory);

            var hosts = db.DomainEntries.Where(x=>x.IsReachable).ToList();

            int total = hosts.Count;

            int parallelThreadsCount = 5;
            Console.WriteLine($"Starting Domain Scan...");
            Parallel.ForEach(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, host =>
            {
                var t = counter.Increment();

                ProcessDomain(host.Domain, host.Port);
                
                Console.WriteLine($"{t}\t{total}");
            }); //close method invocation 

            int xxx = 5;

        }

        public static void ProcessDomain(string domain, int port = 1965)
        {
            DomainAnalyzer analyzer = new DomainAnalyzer(domain, port);
            analyzer.QueryDomain();
            Update(analyzer);
        }

        private static void Update(DomainAnalyzer analyzer)
        {
            using (var db = new DocIndexDbContext(CrawlerOptions.DataDirectory))
            {
                var domain = db.DomainEntries.Where(x => (x.Domain == analyzer.Host && x.Port == analyzer.Port)).FirstOrDefault();

                domain.IsReachable = analyzer.IsReachable;
                domain.ErrorMessage = analyzer.ErrorMessage;
                domain.HasFaviconTxt = analyzer.HasValidFavionTxt;
                domain.HasRobotsTxt = analyzer.HasValidRobotsTxt;
                domain.HasSecurityTxt = analyzer.HasValidSecurityTxt;

                domain.FaviconTxt = analyzer.FaviconTxt;
                domain.RobotsTxt = analyzer.RobotsTxt;
                domain.SecurityTxt = analyzer.SecurityTxt;

                db.SaveChanges();
            }
        }

    }
}
