using System;
using System.ComponentModel.Design.Serialization;
using Gemini.Net;
using RocketForce;

namespace Kennedy.Server.Views.Tools;

internal class UrlTesterView : AbstractView
{
    public UrlTesterView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        GeminiUrl? url = GeminiUrl.MakeUrl(SanitizedQuery);

        Response.Success();
        Response.WriteLine($"# 🐛 URL Checker");

        if (url == null)
        {
            Response.WriteLine("Invalid Gemini URL");
            //RenderInvalidDomain();
            return;
        }

        Response.WriteLine($"=> {url} Requesting: {url}");
        Response.WriteLine($"Requested at {DateTime.UtcNow} UTC");

        GeminiRequestor geminiRequestor = new GeminiRequestor
        {
            AbortTimeout = 15000,
            ConnectionTimeout = 10000,
            MaxResponseSize = 1024 * 1024 * 10
        };

        GeminiResponse response = geminiRequestor.Request(url);

        if (!response.IsAvailable)
        {
            Response.WriteLine("* Connection: ❌");
            Response.WriteLine($"Error establishing connection: {response.Meta}");
            return;
        }
        else
        {
            Response.WriteLine("* Connection: ✅");
        }

        Response.WriteLine($"* Status Code : {response.StatusCode}");
        Response.WriteLine($"* Meta: {response.Meta}");
        if (response.Charset != null)
        {
            Response.WriteLine($"* Charset: {response.Charset}");
        }

        if (response.Language != null)
        {
            Response.WriteLine($"* Language: {FormatLanguage(response.Language)}");
        }

        if (!response.HasBody)
        {
            Response.WriteLine("* Body Size: (No body received)");
        }
        else if (!response.IsBodyTruncated)
        {
            Response.WriteLine($"* Body Size: {FormatSize(response.BodySize)}");
        }
        else
        {
            Response.WriteLine($"* Body Size: > {FormatSize(response.BodySize)}. The exact size is unknown since it exceeded our download limit.");
        }
    }
}
