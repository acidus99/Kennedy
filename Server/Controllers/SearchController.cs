using System;

using Kennedy.Server.Views;
using Kennedy.Server.Views.Search;
using RocketForce;

namespace Kennedy.Server.Controllers
{
    public static class SearchController
    {
        public static void Search(GeminiRequest request, Response response, GeminiServer app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter search query");
                return;
            }
            var view = new ResultsView(request, response, app);
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

        public static void PageInfo(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Entry URL");
                return;
            }
            var view = new PageInfoView(request, response, app);
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
