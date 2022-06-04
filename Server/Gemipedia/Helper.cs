using System;
using System.Net;
using System.IO;

namespace Kennedy.Gemipedia
{
	public static class Helper
	{
        const string GemipediaRoot = "gemini://gemi.dev/cgi-bin/wp.cgi/";

        public static string ArticleUrl(string title)
            => $"{GemipediaRoot}view?{WebUtility.UrlEncode(title)}";

        public static string MediaProxyUrl(string imgUrl)
        {
            //we need to have an extension on the filename of the media proxy URL, so clients
            //will render it as an inline image. Try and figure out what to use, but fall back
            //to a dummy "jpg" if nothing works
            string ext = ".jpg";
            try
            {
                var uri = new Uri(imgUrl);
                ext = Path.GetExtension(uri.AbsolutePath);
            }
            catch (Exception)
            {
                ext = ".jpg";
            }
            return $"{GemipediaRoot}media/media{ext}?{WebUtility.UrlEncode(imgUrl)}";
        }

        public static string SearchUrl(string query)
            => $"{GemipediaRoot}search?{WebUtility.UrlEncode(query)}";
    }
}
