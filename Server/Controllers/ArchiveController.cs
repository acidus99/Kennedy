using System;

using Kennedy.Server.Views.Archive;
using RocketForce;

namespace Kennedy.Server.Controllers
{
    public static class ArchiveController
    {
        public static void UrlHistory(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter specific URL");
                return;
            }
            var view = new UrlHistoryView(request, response, app);
            view.Render();
        }

        public static void UrlFullHistory(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter specific URL");
                return;
            }
            var view = new UrlHistoryView(request, response, app);
            view.ShowAllSnapshots = true;
            view.Render();
        }

        public static void Search(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Search for URLs containing");
                return;
            }

            //if they are searching for a fully qualified URL, redirect them
            if(request.Url.Query.ToLower().StartsWith("gemini://"))
            {
                response.Redirect(RoutePaths.ViewUrlUniqueHistory(request.Url.Query));
                return;
            }

            var view = new SearchResultsView(request, response, app);
            view.Render();
        }

        public static void Cached(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new CachedView(request, response, app);
            view.Render();
        }

        public static void Stats(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new ArchiveStatsView(request, response, app);
            view.Render();
        }
    }
}
