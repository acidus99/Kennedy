using System;

using Kennedy.Server.Views;
using RocketForce;

namespace Kennedy.Server
{
    public static class SearchController
    {
        public static void Search(Request request, Response response, App app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter search query");
                return;
            }
            var view = new SearchResultView(request, response, app);
            view.Render();
        }

        public static void LuckySearch(Request request, Response response, App app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter search query. You will be redirected to the first result.");
                return;
            }
            var view = new LuckyResultView(request, response, app);
            view.Render();
        }

        public static void KnownHosts(Request request, Response response, App app)
        {
            var view = new KnownHostsView(request, response, app);
            view.Render();
        }


    }
}
