namespace Kennedy.Blazer
{	public static class CrawlerOptions
	{
        public readonly static string OutputBase = $"/var/gemini/crawler-out/";

        public static string ErrorLog => OutputBase + "error.txt";

        public static string ConfigDir => OutputBase + "config/";

        public static string DataStore => OutputBase + "crawl-data/";
    }
}
