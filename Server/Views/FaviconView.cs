using System;
using System.Linq;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore.Query.Internal;
using RocketForce;

namespace Kennedy.Server.Views;

internal class FaviconView : AbstractView
{
    public FaviconView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        using var db = new WebDatabaseContext(Settings.Global.DataRoot);
        Response.Success();

        Response.WriteLine("# Favicons! 💃🍪🔮 ");

        var capsules = db.Favicons
            .OrderBy(x => x.Domain)
            .ThenBy(x => x.Port)
            .ToList();

        Response.WriteLine($"{capsules.Count} different capsules know how to have fun! They have a emoji `favicon.txt` file.");
        Response.WriteLine("=> gemini://mozz.us/files/rfc_gemini_favicon.gmi Emoji Favicons in Gemini");
        Response.WriteLine("You should add a favicon to your capsule. Kennedy displays them in search results. Different Gemini clients display too, just like a favicon in a web browser.");
        Response.WriteLine("=> gemini://gemi.dev/gemlog/2022-02-08-favicons.gmi Why I love Emoji Favicons in Gemini");
        Response.WriteLine();
        Response.WriteLine("## All Favicons");
        Response.WriteLine($"Here are all {capsules.Count} from all across Geminispace:");
        int printed = 0;
        foreach (var favicon in capsules)
        {
            printed++;
            if (printed > 20)
            {
                Response.WriteLine();
                printed = 1;
            }
            Response.Write(favicon.Emoji);
        }
        Response.WriteLine();//this one finishing the pending line
        Response.WriteLine();

        Response.WriteLine("Here are the unique favicons, sorted by popularity");
        var favicons = db.Favicons
            .GroupBy(x => x.Emoji)
            .Select(group => new
            {
                Emoji = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();
        foreach (var favicon in favicons)
        {
            string emojis = string.Concat(Enumerable.Repeat(favicon.Emoji, favicon.Count));
            Response.WriteLine($"* {favicon.Count}: {emojis}");
        }
        Response.WriteLine();
        Response.WriteLine($"## Capsules with favicon.txt ({capsules.Count})");
        Response.WriteLine("These capsules are fun. You should be more fun too.");

        int count = 0;
        foreach (var capsule in capsules)
        {
            count++;
            var label = $"{count}. {capsule.Emoji} {capsule.Domain}";
            if (capsule.Port != 1965)
            {
                label += ":" + capsule.Port;
            }
            Response.WriteLine($"=> {capsule.Protocol}://{capsule.Domain}:{capsule.Port} {label}");
        }
    }
}
