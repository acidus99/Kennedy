using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Web;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Models;
using Kennedy.Data;
using RocketForce;
using Kennedy.Archive.Db;
using Microsoft.Data.Sqlite;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Kennedy.Server.Views.Reports
{
    internal class DomainBacklinksView :AbstractView
    {
        public DomainBacklinksView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        WebDatabaseContext db = new WebDatabaseContext(Settings.Global.DataRoot);

        public override void Render()
        {
            var authority = ParseAuthory(SanitizedQuery);

            Response.Success();

            if (!DomainExists(authority.protocol, authority.domain, authority.port))
            {
                RenderUnknownDomain(authority.domain);
                return;
            }

            Response.WriteLine($"# {authority.domain} - ↩️ Backlinks Report");
            Response.WriteLine($"Protocol: {authority.protocol}");
            Response.WriteLine($"Domain: {authority.domain}");
            Response.WriteLine($"Port: {authority.port}");

            List<Backlink> backlinks = GetBacklinks(authority.protocol, authority.domain, authority.port);

            Response.WriteLine($"Backlinks: {backlinks.Count}");

            string prev = "";
            int urlGroupNumber = 0;
            int urlNumber = 0;
            for(int i =0; i < backlinks.Count; i++)
            {
                var backlink = backlinks[i];

                if(backlink.TargetUrl.NormalizedUrl != prev)
                {
                    urlNumber = 0;
                    urlGroupNumber++;
                    prev = backlink.TargetUrl.NormalizedUrl;
                    Response.WriteLine();
                    Response.WriteLine($"## {urlGroupNumber}. Url: {backlink.TargetUrl.Path}");
                    Response.WriteLine($"=> {backlink.TargetUrl} Full Url: {backlink.TargetUrl}");
                    Response.WriteLine($"Status Code: {backlink.StatusCode}");
                    Response.WriteLine($"{CountUrls(backlinks, i)} Backlinks:");
                }

                urlNumber++;

                Response.Write($"=> {backlink.SourceUrl} {urlNumber}. \"{backlink.SourceUrl}\"");
                if(!string.IsNullOrEmpty(backlink.LinkText))
                {
                    Response.Write($" with link \"{backlink.LinkText}\"");
                }
                Response.WriteLine();
            }
        }

        private void RenderUnknownDomain(string domain)
        {
            Response.WriteLine($"# ↩️ Backlinks Report");
            Response.WriteLine("Sorry, Kennedy has no information about this domain:");
            Response.WriteLine($"```");
            Response.WriteLine($"{domain}");
            Response.WriteLine($"```");
            Response.WriteLine($"=> {RoutePaths.DomainBacklinksRoute} Try another Domain");
        }

        //counts how many items in a row have the same url, starting from an index
        private int CountUrls(List<Backlink> backlinks, int currIndex)
        {
            int count = 0;

            string targetUrl = backlinks[currIndex].TargetUrl.NormalizedUrl;

            for (; currIndex < backlinks.Count; currIndex++)
            {
                var currUrl = backlinks[currIndex].TargetUrl.NormalizedUrl;
                if (targetUrl != currUrl)
                {
                    break;
                }
                count++;
            }
            return count;
        }

        private (string protocol, string domain, int port) ParseAuthory(string s)
        {
            s = s.ToLower();
            int index = s.IndexOf(':');

            if (index >= 1 && s.Length > index + 1)
            {
                try
                {
                    return ("gemini", s.Substring(0, index), Convert.ToInt32(s.Substring(index + 1)));
                }
                catch (Exception)
                {
                }
            }
            return ("gemini", s, 1965);
        }

        private bool DomainExists(string protocol, string domain, int port)
        {
            return db.Documents
                .Where(x => x.Protocol == protocol && x.Domain == domain && x.Port == port)
                .FirstOrDefault() != null;
        }

        private List<Backlink> GetBacklinks(string protocol, string domain, int port)
        {
            var ret = new List<Backlink>();
            try
            {
                using (var connection = db.Database.GetDbConnection())
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(
@"
select source.Url as surl, Links.LinkText as linktext, target.Url as turl, target.StatusCode as sc
FROM Documents as source
join Links on
source.UrlID = Links.SourceUrlID
join Documents as target
on Links.TargetUrlID = target.UrlID
Where Links.IsExternal = true
and target.Domain = $domain and target.Protocol = $protocol and target.Port = $port
order by target.Url
", (SqliteConnection?)connection);

                    cmd.Parameters.Add(new SqliteParameter("domain", domain));
                    cmd.Parameters.Add(new SqliteParameter("protocol", protocol));
                    cmd.Parameters.Add(new SqliteParameter("port", port));
                    SqliteDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        int ordinal = reader.GetOrdinal("linktext");

                        string? linkText = reader.IsDBNull(ordinal) ?
                                            null : reader.GetString(ordinal).Trim();

                        ret.Add(new Backlink
                        {
                            SourceUrl = new GeminiUrl(reader.GetString(reader.GetOrdinal("surl"))),
                            TargetUrl = new GeminiUrl(reader.GetString(reader.GetOrdinal("turl"))),
                            StatusCode = reader.GetInt32(reader.GetOrdinal("sc")),
                            LinkText = linkText
                        });
                    }
                    return ret;
                }
            }
            catch (Exception)
            {

            }
            return ret;
        }

        private class Backlink
        {
            public required GeminiUrl SourceUrl { get; set; }

            public required GeminiUrl TargetUrl { get; set; }

            public string? LinkText { get; set; }

            public required int StatusCode { get; set; }
        }
    }
}
