using System;

using Kennedy.Server.Views.Search;
using RocketForce;

namespace Kennedy.Server.Controllers
{
    public static class ImageSearchController
    {
        public static void Search(GeminiRequest request, Response response, GeminiServer app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter image search query");
                return;
            }
            var view = new ImageResultsView(request, response, app);
            view.Render();
        }
    }
}
