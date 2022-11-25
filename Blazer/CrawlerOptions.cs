namespace Kennedy.Blazer
{	public static class CrawlerOptions
	{
        public readonly static string OutputBase = $"/var/gemini/{DateTime.Now.ToString("yyyy-MM-dd (mm)")}/";

        public static string ErrorLog => OutputBase + "error.txt";


    }
}
