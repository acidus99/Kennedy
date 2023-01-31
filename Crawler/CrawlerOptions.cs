namespace Kennedy.Blazer
{	public static class CrawlerOptions
	{
        public static string OutputBase = "~/kennedy-capsule/crawler-out/";

        public static string Logs => OutputBase + "logs/";

        public static string ErrorLog => Logs + "error.txt";

        public static string ConfigDir => OutputBase + "config/";

        public static string DataStore => OutputBase + "crawl-data/";

        public static string PublicRoot => OutputBase + "public_root/";
    }
}
