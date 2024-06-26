﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using Gemini.Net;
using Kennedy.Archive.Db;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Rewrites the links in a Gemtext file
/// </summary>
public class GemtextRewriter
{
    static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

    public string Rewrite(Snapshot snapshot, string bodyText)
    {
        if (snapshot.Url == null)
        {
            throw new ArgumentNullException(nameof(snapshot), "Snapshot URL cannot be null");
        }

        var sb = new StringBuilder();
        bool inPreformatted = false;

        foreach (var line in bodyText.Split("\n"))
        {
            if (line.StartsWith("```"))
            {
                sb.AppendLine(line);
                inPreformatted = !inPreformatted;
                continue;
            }
            if (!inPreformatted && line.StartsWith("=>"))
            {
                sb.AppendLine(ReWriteLinkLine(line, snapshot));
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    private string ReWriteLinkLine(string line, Snapshot snapshot)
    {
        var match = linkLine.Match(line);

        if (!match.Success)
        {
            return line;
        }

        //attempt to make a gemini URL out of the original URL
        //if this fails, the original URL was a fully qualitied URL to another
        //scheme, so we have nothing to rewrite
        var geminiUrl = GeminiUrl.MakeUrl(snapshot.Url!.GeminiUrl, match.Groups[1].Value);
        if (geminiUrl == null)
        {
            return line;
        }

        var linkText = (match.Groups.Count > 2) ? match.Groups[2].Value.Trim() : "";
        if (linkText == "")
        {
            //if no link text, use the original URL so client's don't display the URL to the cached view
            linkText = match.Groups[1].Value;
        }

        return $"=> {RoutePaths.ViewCached(geminiUrl, snapshot.Captured)} {linkText}";
    }
}