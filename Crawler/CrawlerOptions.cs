namespace Kennedy.Crawler
{	public static class CrawlerOptions
	{
        public static string ConfigDir => "config/";
        public static string Logs => OutputBase + "logs/";
        public static string OutputBase = "~/kennedy-capsule/crawler-out/";
        public static string RejectionsLog => Logs + "rejected-urls.tsv";
        public static string ResponsesLog => Logs + "response.tsv";
        public static string WarcDir => OutputBase + "warcs/";
    }
}
