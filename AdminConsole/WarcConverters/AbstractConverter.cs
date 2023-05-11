﻿namespace Kennedy.AdminConsole.WarcConverters;

using System;
using System.Diagnostics;
using Kennedy.Warc;
using Kennedy.AdminConsole.Db;

public abstract class AbstractConverter
{
    protected Stopwatch stopwatch;

    protected int RecordsProcessed = 0;

    protected int RecordsWritten = 0;

    protected string CrawlLocation;

    protected GeminiWarcCreator WarcCreator { get; private set; }

    protected abstract string ConverterName { get; }

    public AbstractConverter(GeminiWarcCreator warcCreator, string crawlLocation)
	{
        CrawlLocation = crawlLocation;
        WarcCreator = warcCreator;
        stopwatch = new Stopwatch();
	}

    public void WriteToWarc()
    {
        Console.WriteLine($"Starting {ConverterName} on {CrawlLocation}");
        stopwatch.Start();
        ConvertCrawl();
        stopwatch.Stop();
        Console.WriteLine($"Completed!");
        Console.WriteLine($"Time:\t{stopwatch.Elapsed.TotalSeconds}");
        Console.WriteLine($"Processed:\t{RecordsProcessed}");
        Console.WriteLine($"Written:\t{RecordsWritten}");
    }

    protected abstract void ConvertCrawl();

    protected string GetJustMimetype(string meta)
    {
        if (meta.Length == 0)
        {
            return "text/gemini";
        }
        int paramIndex = meta.IndexOf(";");
        return  (paramIndex > 0) ?
                meta.Substring(0, paramIndex) :
                meta;
    }

    protected bool IsTruncated(SimpleDocument document)
    {
        if (document.BodySkipped)
        {
            return true;
        }

        if (document.Status == 20 && document.Meta.StartsWith("Requestor aborting due to reaching max download"))
        {
            return true;
        }
        return false;
    }
}
