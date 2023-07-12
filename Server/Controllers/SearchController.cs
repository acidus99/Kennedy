using System;

using Kennedy.Server.Views;
using Kennedy.Server.Views.Archive;
using Kennedy.Server.Views.Search;
using RocketForce;

namespace Kennedy.Server.Controllers
{
    public static class SearchController
    {
        public static void Search(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter search query");
                return;
            }
            var view = new ResultsView(request, response, app);
            view.Render();
        }

        public static void SiteSearch(GeminiRequest request, Response response, GeminiServer app)
        {
            //are they making a new site search?
            if(request.Route == RoutePaths.SiteSearchRoute)
            {
                if (!request.Url.HasQuery)
                {
                    response.Input($"Enter domain name to create Site Search link.");
                    return;
                }
                var view = new SiteSearchView(request, response, app);
                view.Render();
                return;
            }

            //pull out the capsule

            string? capsule = Helpers.SiteSearch.GetSite(request.Route);
            if(capsule == null)
            {
                response.Error("Invalid domain name");
                return;
            }

            if(!request.Url.HasQuery)
            {
                response.Input($"Search '{capsule}' for?");
                return;
            }

            response.Redirect(RoutePaths.Search($"site:{capsule} " + request.Url.Query));
        }

        public static void Stats(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new SearchStatsView(request, response, app);
            view.Render();
        }

        public static void LuckySearch(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter search query. You will be redirected to the first result.");
                return;
            }
            var view = new LuckyResultsView(request, response, app);
            view.Render();
        }

        public static void UrlInfo(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Entry URL");
                return;
            }
            var view = new UrlInfoView(request, response, app);
            view.Render();
        }

        public static void KnownHosts(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new KnownHostsView(request, response, app);
            view.Render();
        }

        public static void SecurityTxt(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new SecurityTxtView(request, response, app);
            view.Render();
        }
    }
}
