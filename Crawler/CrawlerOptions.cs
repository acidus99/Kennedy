namespace Kennedy.Crawler
{	public static class CrawlerOptions
	{
        public static string ConfigDir => "config/";
        public static string Logs => OutputBase + "logs/";
        public static string OutputBase = "~/kennedy-capsule/crawler-out/";
        public static string RejectionsLog => Logs + "rejected-urls.txt";
        public static string ResponsesLog => Logs + "urls.txt";
        public static string WarcDir => OutputBase + "warcs/";
    }
}
