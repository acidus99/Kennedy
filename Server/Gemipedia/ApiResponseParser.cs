using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kennedy.Gemipedia;

/// <summary>
/// Wikipedia API parser
/// </summary>
internal static class ApiResponseParser
{
    public static List<ArticleSummary> ParseSearchResponse(string json)
    {
        List<ArticleSummary> ret = new List<ArticleSummary>();
        var response = JObject.Parse(json);

        var resultsArray = response["pages"] as JArray;
        if (resultsArray == null)
        {
            return ret;
        }

        foreach (JObject result in resultsArray)
        {
            ret.Add(new ArticleSummary
            {
                Title = Cleanse(result["title"] as JToken),
                Description = Cleanse(result["description"]),
            });
        }
        return ret;
    }

    private static string Cleanse(JToken? token)
        => token?.ToString() ?? "";
}