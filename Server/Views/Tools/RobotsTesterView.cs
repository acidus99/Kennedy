using System;
using System.Linq;
using System.Text.RegularExpressions;
using Gemini.Net;
using Kennedy.Data.RobotsTxt;
using RocketForce;

namespace Kennedy.Server.Views.Tools;

internal class RobotsTesterView : AbstractView
{
    private static readonly Regex DomainRegex = new Regex(
        @"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex TldRegex = new Regex(
        @"\.[a-z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;
        return DomainRegex.IsMatch(domain) && TldRegex.IsMatch(domain);
    }

    public RobotsTesterView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();
        Response.WriteLine($"# 🤖 Robots.txt Validator");

        string Domain = SanitizedQuery;
        if (!IsValidDomain(Domain))
        {
            Response.WriteLine("Invalid Gemini URL");
            return;
        }

        GeminiUrl url = new($"gemini://{Domain}/robots.txt");

        Response.WriteLine($"=> {url} Testing: {url}");
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
            Response.WriteLine($"Failed: Could not establishing connection due to '{response.Meta}'");
            return;
        }
        else
        {
            Response.WriteLine("* Connection: ✅");
        }

        if (!response.IsSuccess)
        {
            Response.WriteLine(($"* Status Code: {response.StatusCode} ❌"));
            Response.WriteLine("Failed: /robots.txt must return status code of 20");
            return;
        }
        else
        {
            Response.WriteLine(($"* Status Code: {response.StatusCode} ✅"));
        }

        if (response.MimeType! != "text/plain")
        {
            Response.WriteLine(($"* MIME Type: {response.MimeType!} ⚠️"));
            Response.WriteLine("Warning: Didn't get a `text/plain` MIME type. Some systems may not accept this.");
        }
        else
        {
            Response.WriteLine(($"* MIME Type: {response.MimeType!} ✅"));
        }

        if (!response.HasBody)
        {
            Response.WriteLine(($"* Has response body?: ❌"));
            Response.WriteLine("Failed: No content in body to parse for robots.txt rules.");
        }
        else
        {
            Response.WriteLine(($"* Has response body?: ✅"));
            RobotsTxtParser parser = new RobotsTxtParser();

            RobotsTxtFile robotsTxtFile = parser.Parse(response.BodyText);

            if (parser.Warnings.Any())
            {
                Console.WriteLine($"* {parser.Warnings.Count()} Parsing Warnings/Errors");
                int i = 1;
                foreach (var warning in parser.Warnings)
                {
                    Console.WriteLine($"* {i}. {warning}");
                    i++;
                }
            }

            if (parser.Warnings.Any())
            {
                Response.WriteLine($"* Compiles with Gemini's subset of Robots.txt?: ❌");
                Response.WriteLine($"## Unsupported Rules/directives - {parser.Warnings.Count()}");
                foreach (var warning in parser.Warnings)
                {
                    Response.WriteLine("* " + warning);
                }
            }
            else
            {
                Response.WriteLine($"* Compiles with Gemini's subset of Robots.txt?: ✅");
            }

            if (robotsTxtFile.HasValidRules)
            {
                Response.WriteLine($"* Has any valid Rules!: ✅");
                PrintRules(robotsTxtFile);
            }

            var wwww = parser.Warnings.ToList();


        }
    }

    private void PrintRules(RobotsTxtFile robotsTxtFile)
    {
        Response.WriteLine("## Robots.txt Rules");
        foreach (string ua in robotsTxtFile.Rules.Keys)
        {
            Response.WriteLine($"### User-Agent: {ua}");
            foreach (DenyRule rule in robotsTxtFile.Rules[ua])
            {
                Response.WriteLine($"* {rule.Line} (Line #{rule.LineNumber})");
            }
        }
    }
}
