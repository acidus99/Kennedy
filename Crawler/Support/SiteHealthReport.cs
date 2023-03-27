﻿using System;
using System.Web;

using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.CrawlData.Db;

namespace Kennedy.Crawler.Support
{
	public class SiteHealthReport
	{

		DocIndexDbContext db;

		StreamWriter fout;

        public SiteHealthReport(string storageDir)
		{
			db = new DocIndexDbContext(storageDir);
		}

		public void WriteReport(string filename, string domain)
		{
			fout = new StreamWriter(filename);
			fout.WriteLine("# 🩺 Site Health Report");
            fout.WriteLine($"## {domain}");

			var docs = db.DocEntries
				.Where(x => x.Domain == domain);

			var totalDocs = docs.Count();

			var docsWithProblems = docs.Where(x => x.Domain == domain &&
											x.ConnectStatus != ConnectStatus.Error &&
											x.Status >= 40 &&
											x.Status < 60).ToArray();

			fout.WriteLine($"* Total URLs: {totalDocs}");
            fout.WriteLine($"* URLs with problems: {docsWithProblems.Count()}");

			fout.WriteLine("## Issues");
			int counter = 1;
            foreach (var doc in docsWithProblems)
            {
				var geminiUrl = new GeminiUrl(doc.Url);

				fout.WriteLine($"### {counter} Code {doc.Status} on {geminiUrl.Path} ");
				fout.WriteLine($"=> {doc.Url}");
				fout.WriteLine("Incoming Links:");

				var links = db.LinkEntries.Include(x => x.SourceUrl)
                    .Where(x => x.TargetUrlID == doc.UrlID);

                foreach (var link in links)
                {
                    fout.WriteLine($"=> {link.SourceUrl.Url} Link \"{link.LinkText}\" on {link.SourceUrl.Url}");
                }

				fout.WriteLine();
				counter++;
            }


            fout.Close();
        }

		private string UrlIDToGraphID(long urlID)
			=> "ID_" + urlID.ToString().Replace('-', '_');

	}
}
