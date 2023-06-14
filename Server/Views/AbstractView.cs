using System;
using System.IO;
using System.Text;

using Gemini.Net;
using Kennedy.Archive.Db;
using Kennedy.SearchIndex.Models;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal abstract class AbstractView
    {
        protected GeminiRequest Request;
        protected Response Response;
        protected GeminiServer App;

        public AbstractView(GeminiRequest request, Response response, GeminiServer app)
        {
            Request = request;
            Response = response;
            App = app;
        }

        public abstract void Render();

        //removes whitepsace so a user query cannot inject new gemtext lines into the output
        protected string SanitizedQuery
            => Request.Url.Query.Replace("\r", "").Replace("\n", "").Trim();

        protected string FormatCount(int i)
            => i.ToString("N0");

        protected string FormatCount(long i)
            => i.ToString("N0");

        protected string FormatDomain(string domain, string? favicon)
            => (favicon != null) ? $"{favicon} {domain}" : $"{domain}";

        protected string FormatSize(int bodySize)
            => FormatSize(Convert.ToInt64(bodySize));

        protected string FormatSize(long bodySize)
        {
            if (bodySize < 1024)
            {
                return $"{bodySize.ToString("N0")} B";
            }

            return $"{Math.Round(((double)bodySize) / ((double)1024)).ToString("N0")} KiB";
        }

        protected string FormatUrl(GeminiUrl url)
        {
            var parts = (url.Hostname + url.Path).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var ret = string.Join(" › ", parts);
            if (ret.Length > 80)
            {
                ret = ret.Substring(0, 80) + '…';
            }
            return ret;
        }

        protected string FormatFilename(GeminiUrl url)
            => (url.Filename.Length > 0) ?
                url.Filename :
                "/";

        protected string FormatLanguage(string twoLetterISOLanguageName)
        {
            var culture = new System.Globalization.CultureInfo(twoLetterISOLanguageName);
            return culture.DisplayName;
        }
    }
}
