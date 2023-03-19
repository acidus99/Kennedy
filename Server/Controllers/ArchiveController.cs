using System;

using Kennedy.Server.Views.Archive;
using RocketForce;

namespace Kennedy.Server.Controllers
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
            var view = new UrlHistoryView(request, response, app);
            view.Render();
        }

        public static void Cached(GeminiRequest request, Response response, GeminiServer app)
        {
            var view = new CachedView(request, response, app);
            view.Render();
        }

    }
}
