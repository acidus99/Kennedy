using System;

using Kennedy.Server.Views;
using RocketForce;

namespace Kennedy.Server
{
    public static class ArchiveController
    {
        public static void Search(GeminiRequest request, Response response, GeminiServer app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter search query");
                return;
            }
            var view = new DorleanResultView(request, response, app);
            view.Render();
        }

        public static void Cached(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new CachedView(request, response, app);
            view.Render();
        }

    }
}
