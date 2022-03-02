using System;
using System.IO;
using Gemini.Net;
using RocketForce;
namespace Kennedy.Server.Views
{
    internal abstract class AbstractView
    {
        protected Request Request;
        protected Response Response;
        protected App App;

        protected TextWriter Out { get; private set; }

        public AbstractView(Request request, Response response, App app)
        {
            Request = request;
            Response = response;
            App = app;
        }

        public abstract void Render();

        //removes whitepsace so a user query cannot inject new gemtext lines into the output
        protected string SanitizedQuery
            => Request.Url.Query.Replace("\r", "").Replace("\n", "").Trim();


        protected string FormatDomain(string domain, string favicon)
            => (favicon.Length > 0) ? $"{favicon} {domain}" : $"{domain}";

        protected string FormatSize(int bodySize)
        {
            if (bodySize < 1024)
            {
                return $"{bodySize} B";
            }

            return $"{Math.Round(((double)bodySize) / ((double)1024))} KB";
        }

        protected string FormatPageTitle(GeminiUrl url, string title)
        {
            if (title.Trim().Length > 0)
            {
                return title;
            }
            return $"{url.Hostname}{url.Path}";
        }

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
