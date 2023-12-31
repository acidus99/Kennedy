using System.Net;

namespace Kennedy.Gemipedia;

	public static class Helper
	{
    const string GemipediaRoot = "gemini://gemi.dev/cgi-bin/wp.cgi/";

    public static string ArticleUrl(ArticleSummary article)
        => $"{GemipediaRoot}view?{WebUtility.UrlEncode(article.Title)}";
}
