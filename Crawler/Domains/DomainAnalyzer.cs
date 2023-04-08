using System;

using Gemini.Net;


using Kennedy.SearchIndex.Web;
using Kennedy.Crawler.Crawling;
using Kennedy.Crawler.Utils;
using Kennedy.Data;


namespace Kennedy.Crawler.Domains
{
	public class DomainAnalyzer
	{
		bool IsRunning = true;

		bool StayAlive = true;

        IWebDatabase WebDB;
		IWebCrawler Crawler;

		object locker;

		Queue<Tuple<string, int, bool>> Pending;
		Bag<string> SeenAuthorities;


		public DomainAnalyzer(IWebDatabase webDB, IWebCrawler crawler)
		{
			locker = new object();
			WebDB = webDB;
			Pending = new Queue<Tuple<string, int, bool>>();
			SeenAuthorities = new Bag<string>();
			Crawler = crawler;
		}

		public void Start()
		{
            var threadDelegate = new ThreadStart(AnalyzeDomains);
            var newThread = new Thread(threadDelegate);
            newThread.Name = $"Domain Analzyer";
            newThread.Start();
        }

		public void Stop()
		{
			StayAlive = false;
			while(IsRunning)
			{
				Thread.Sleep(500);
			}
		}

		public void AddDomain(string hostname, int port, bool isReachable)
		{

			var authority = new Tuple<string, int, bool>(hostname, port, isReachable);
			var key = GetKey(authority);

			lock(locker)
			{
				if(!SeenAuthorities.Contains(key))
				{
					SeenAuthorities.Add(key);
					Pending.Enqueue(authority);
				}
			}
		}

		private string GetKey(Tuple<string, int, bool> authority)
			=> $"{authority.Item1}:{authority.Item2}";


		private void AnalyzeDomains()
		{
			IsRunning = true;
			do
			{
				int count = Pending.Count;
				if (count > 0)
				{
					Console.WriteLine("Domain Analyzer Pending: " + count);

                    var authority = Pending.Dequeue();
					AnalyzeDomain(authority.Item1, authority.Item2, authority.Item3);
				}
				else
				{
                    Thread.Sleep(1000);
                }
				Thread.Sleep(100);
			} while (StayAlive || Pending.Count > 0);
			IsRunning = false;
        }

		private void AnalyzeDomain(string host, int port, bool isReachable)
		{
			Console.WriteLine("Analyzing " + host);

			FilesFetcher fetcher = new FilesFetcher(host, port, Crawler);
			fetcher.FetchFiles(isReachable);
            
            WebDB.StoreDomain(new DomainInfo
            {
                Domain = host,
                Port = port,

                IsReachable = fetcher.IsReachable,

                HasFaviconTxt = fetcher.HasValidFavionTxt,
                HasRobotsTxt = fetcher.HasValidRobotsTxt,
                HasSecurityTxt = fetcher.HasValidSecurityTxt,

                FaviconTxt = fetcher.FaviconTxt,
                RobotsTxt = fetcher.RobotsTxt,
                SecurityTxt = fetcher.SecurityTxt
            });
        }
	}
}
