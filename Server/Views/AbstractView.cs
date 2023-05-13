using System;
using System.IO;
using Gemini.Net;
using RocketForce;
namespace Kennedy.Server.Views
{
    internal abstract class AbstractView
    {
        protected GeminiRequest Request;
        protected Response Response;
        protected GeminiServer App;

        protected TextWriter Out { get; private set; }

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

        protected string FormatDomain(string domain, string favicon)
            => (favicon.Length > 0) ? $"{favicon} {domain}" : $"{domain}";

        protected string FormatSize(int bodySize)
            => FormatSize(Convert.ToInt64(bodySize));

        protected string FormatSize(long bodySize)
        {
            if (bodySize < 1024)
            {
                return $"{bodySize.ToString("N0")} B";
            }

            return $"{Math.Round(((double)bodySize) / ((double)1024)).ToString("N0")} KB";
        }

        protected string FormatPageTitle(GeminiUrl url)
            => $"{url.Hostname}{url.Path}";

        protected string FormatPageTitle(GeminiUrl url, string title)
            => (title.Trim().Length > 0) ? title : FormatPageTitle(url);

        protected string FormatLanguage(string language)
        {
            switch (language)
            {
                case "dan":
                    return "Danish";
                case "deu":
                    return "German";
                case "eng":
                    return "English";
                case "fra":
                    return "French";
                case "ita":
                    return "Italian";
                case "jpn":
                    return "Japanese";
                case "kor":
                    return "Korean";
                case "nld":
                    return "Dutch";
                case "nor":
                    return "Norwegian";
                case "por":
                    return "Portuguese";
                case "rus":
                    return "Russian";
                case "spa":
                    return "Spanish";
                case "swe":
                    return "Swedish";
                case "zho":
                    return "Chinese";
                default:
                    return "";
            }
        }
    }
}
