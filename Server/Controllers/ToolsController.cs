using Kennedy.Server.Views.Tools;
using RocketForce;

namespace Kennedy.Server.Controllers;

public static class ToolsController
{
    public static void RobotsTester(GeminiRequest request, Response response, GeminiServer app)
    {
        if (!request.Url.HasQuery)
        {
            response.Input("Entry domain to test for valid robots.txt?");
            return;
        }
        var view = new RobotsTesterView(request, response, app);
        view.Render();
    }
    public static void UrlTester(GeminiRequest request, Response response, GeminiServer app)
    {
        if (!request.Url.HasQuery)
        {
            response.Input("Entry URL to test?");
            return;
        }
        var view = new UrlTesterView(request, response, app);
        view.Render();
    }
}
