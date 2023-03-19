using System;

using Kennedy.Server.Views;
using RocketForce;

namespace Kennedy.Server
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
            var view = new SearchResultView(request, response, app);
            view.Render();
        }

        public static void LuckySearch(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter search query. You will be redirected to the first result.");
                return;
            }
            var view = new LuckyResultView(request, response, app);
            view.Render();
        }

        public static void PageInfo(GeminiRequest request, Response response, GeminiServer app)
        {
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
