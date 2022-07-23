using System;

using Kennedy.Server.Views;
using RocketForce;

namespace Kennedy.Server
{
    public static class ImageSearchController
    {
        public static void Search(Request request, Response response, App app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter image search query");
                return;
            }
            var view = new ImageSearchResultsView(request, response, app);
            view.Render();
        }
    }
}
