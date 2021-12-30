using System;
using HashDepot;
using System.Text;

namespace Gemi.Net
{
    public class GemiUrl :IEquatable<GemiUrl>
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

        private ulong? docID;

        /// <summary>
        /// Get DocID from a URL. This happens by normalizing the URL and hashing it
        /// </summary>
        public ulong DocID
        {
            get
            {
                if (!docID.HasValue)
                {
                    docID = XXHash.Hash64(Encoding.UTF8.GetBytes(NormalizedUrl));
                }
                return docID.Value;
            }
        }

        public int Port => (_url.Port > 0) ? _url.Port : 1965;

        public string Authority => $"{Hostname}:{Port}";

        //TODO: handle punycode/IDN
        public string Hostname => _url.DnsSafeHost;

        public string Path => _url.AbsolutePath;

        public string Filename => System.IO.Path.GetFileName(Path);

        public string FileExtension
        {
            get
            {
                var ext = System.IO.Path.GetExtension(Path);
                return (ext.Length > 1) ? ext.Substring(1) : ext;
            }
        }

        public string NormalizedUrl
            => $"gemini://{Hostname}:{Port}{Path}";

        public override string ToString()
            => NormalizedUrl;

        //Handles resolving relative URLs
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

        //ultimately 2 URLs are equal if their DocID is equal
        public bool Equals(GemiUrl other)
            => other != null && DocID.Equals(other.DocID);

        public override bool Equals(object obj)
            => Equals(obj as GemiUrl);

        public override int GetHashCode()
            => DocID.GetHashCode();
    }
}
