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

        public static void PageInfo(Request request, Response response, App app)
        {
            var view = new PageInfoView(request, response, app);
            view.Render();
        }

        public static void KnownHosts(Request request, Response response, App app)
        {
            var view = new KnownHostsView(request, response, app);
            view.Render();
        }

        public static void SecurityTxt(Request request, Response response, App app)
        {
            var view = new SecurityTxtView(request, response, app);
            view.Render();
        }

        public static void Cached(Request request, Response response, App app)
        {
            var view = new CachedView(request, response, app);
            view.Render();
        }

        public static void DeloreanSearch(Request request, Response response, App app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter Gemini Url");
                return;
            }
            var view = new DorleanResultView(request, response, app);
            view.Render();
        }

    }
}
