using System;

using Kennedy.Server.Views.Reports;
using RocketForce;

namespace Kennedy.Server.Controllers
{
    public static class ReportsController
    {
        public static void SiteHealth(GeminiRequest request, Response response, GeminiServer app)
        {
            if(!request.Url.HasQuery)
            {
                response.Input("Enter Domain");
                return;
            }
            var view = new SiteHealthView(request, response, app);
            view.Render();
        }

        public static void DomainBacklinks(GeminiRequest request, Response response, GeminiServer app)
        {
            if (!request.Url.HasQuery)
            {
                response.Input("Enter Domain");
                return;
            }
            var view = new DomainBacklinksView(request, response, app);
            view.Render();
        }

    }
}
