using RocketForce;
using Gemini.Net;

namespace Kennedy.Server.Views.Archive
{
    internal class SiteSearchView :AbstractView
    {
        public SiteSearchView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            string capsule = SanitizedQuery;
                
            Response.Success();
            Response.WriteLine($"# 🎯 Kennedy Site Search");

            //if they gave us a gemini URL, be cool and work with that
            if(capsule.StartsWith("gemini://"))
            {
                capsule = ExtractDomain(capsule);
            }

            if (!Helpers.SiteSearch.IsValidCapsuleName(capsule))
            {
                Response.WriteLine("Invalid domain name. Please enter a regular domain name.");
                Response.WriteLine($"=> {RoutePaths.SiteSearchRoute} Try again");
                return;
            }

            Response.WriteLine($"Here is a URL that will allow visitors to search the capsule '{capsule}':");
            Response.WriteLine("```");
            Response.WriteLine($"{RoutePaths.SiteSearch(capsule)}");
            Response.WriteLine("```");
            Response.WriteLine();
            Response.WriteLine("You can test it with the link below:");
            Response.WriteLine($"=> {RoutePaths.SiteSearch(capsule)} 🔍 Search this capsule");
            Response.WriteLine();
            Response.WriteLine("Add this link line to your capsule to easily add site search to your capsule:");
            Response.WriteLine("```");
            Response.WriteLine($"=> {RoutePaths.SiteSearch(capsule)} 🔍 Search this capsule");
            Response.WriteLine("```");
        }

        private string ExtractDomain(string url)
        {
            GeminiUrl? gurl = GeminiUrl.MakeUrl(url);
            if(gurl != null)
            {
                return gurl.Hostname;
            }
            return "";
        }
    }
}
