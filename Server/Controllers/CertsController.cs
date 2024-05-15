using Kennedy.Server.Views.Certs;
using RocketForce;

namespace Kennedy.Server.Controllers;

public static class CertsController
{
    public static void Check(GeminiRequest request, Response response, GeminiServer app)
    {
        if (!request.Url.HasQuery)
        {
            response.Input("URL or Domain to check?");
            return;
        }
        var view = new CertsCheckView(request, response, app);
        view.Render();
    }
}