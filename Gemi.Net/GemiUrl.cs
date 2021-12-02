using System;
namespace Gemi.Net
{
    public class GemiUrl
    {
        public Uri _url;

        public GemiUrl(string url)
            : this(new Uri(url)) { }

        private GemiUrl(Uri url)
        {
            _url = url;
            if(!_url.IsAbsoluteUri)
            {
                throw new ApplicationException("URL was not absolute!");
            }
            if(_url.Scheme != "gemini")
            {
                throw new ApplicationException("Attempting to create a non-Gemini URL!");
            }

            //TODO: Add URL normalization logic per RFC 3986
            //TODO: add URL restrictions in Gemini spec (no userinfo, etc)
        }

        public int Port => (_url.Port > 0) ? _url.Port : 1965;

        //TODO: handle punycode/IDN
        public string Hostname => _url.DnsSafeHost;

        public string Path => _url.AbsolutePath;

        public string NormalizedUrl
            => $"gemini://{Hostname}:{Port}{Path}";

        public override string ToString()
            => NormalizedUrl;

        public static GemiUrl MakeUrl(GemiUrl request, string foundUrl)
        {
            Uri newUrl = null;
            try
            {
                newUrl = new Uri(request._url, foundUrl);
            } catch(Exception)
            {
                return null;
            }
            return (newUrl.Scheme == "gemini") ? new GemiUrl(newUrl) : null;
        }
    }
}
