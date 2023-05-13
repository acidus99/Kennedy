namespace Kennedy.Crawler
{	public static class CrawlerOptions
	{
        public static string OutputBase = "~/kennedy-capsule/crawler-out/";

        public static string Logs => OutputBase + "logs/";

        public static string ErrorLog => Logs + "error.txt";

        public static string ConfigDir => "config/";

        public static string WarcDir => OutputBase + "warcs/";

    }
}
