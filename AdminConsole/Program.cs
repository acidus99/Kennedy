using System.Linq;

using Microsoft.EntityFrameworkCore;
using HashDepot;

using Gemini.Net;

using Kennedy.AdminConsole.Importers;
using Kennedy.Archive;
using Kennedy.Data;
using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;

namespace Kennedy.AdminConsole
{
    class Program
    {
        static string ArchiveDBPath
            => DataRootDirectory + "archive.db";

        static string PacksPath
            => DataRootDirectory + "Packs" + Path.DirectorySeparatorChar;

        static string DataRootDirectory = "";
        static string Operation = "";
        static string argument = "";

        static void Main(string[] args)
        {
            if (!ValidateArgs(args))
            {
                return;
            }

            switch (Operation)
            {
                case "add":
                    Console.WriteLine("Adding to archive");
                    AddCrawlToArchive(argument);
                    break;

                case "delete":
                    DeleteFromCrawl(argument);
                    break;

                case "robots":
                    ExcludeFilesFromArchive();
                    break;

            }
        }

        static string EnsureTrailingSlash(string path)
            => (path.EndsWith(Path.DirectorySeparatorChar)) ?
                path :
                path + Path.DirectorySeparatorChar;

        static bool ValidateArgs(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("not enough arguments");
                Console.WriteLine("Usage: [operation] [path to archive root] [[additional args]]");
                return false;
            }

            Operation = args[0].ToLower();
            DataRootDirectory = EnsureTrailingSlash(args[1]);

            if(!File.Exists(ArchiveDBPath))
            {
                Console.WriteLine($"Could not locate archive database at '{ArchiveDBPath}'");
                return false;
            }
            if (!Directory.Exists(PacksPath))
            {
                Console.WriteLine($"Could not locate Packs directory at '{PacksPath}'");
                return false;
            }

            switch(Operation)
            {
                case "add":
                    {
                        if(args.Length != 3)
                        {
                            Console.WriteLine($"Not enough arguments for operation {Operation}");
                            Console.WriteLine($"Usage: {Operation} [path to archive root] path to crawler output to add");
                            return false;
                        }

                        argument = EnsureTrailingSlash(args[2]);
                        if (!Directory.Exists(argument))
                        {
                            Console.WriteLine($"Could need file valid crawler output at '{argument}'");
                            return false;
                        }
                        return true;
                    }

                case "delete":
                    {
                        argument = args[2];
                        return true;
                    }

                case "robots":
                    {
                        return true;
                    }

                default:
                    Console.WriteLine($"Unknown operation '{Operation}'");
                    return false;

            }
        }

        static void AddCrawlToArchive(string crawlLocation)
        {
            var archiver = new Archiver(ArchiveDBPath, PacksPath);

            DomainImporter importer = new DomainImporter(archiver, crawlLocation);
            importer.Import();

            ModernImporter modern = new ModernImporter(archiver, crawlLocation);
            modern.Import();
        }

        static void DeleteFromCrawl(string pattern)
        {

            Archiver archiver = new Archiver(ArchiveDBPath, PacksPath);
            SearchStorageWrapper wrapper = new SearchStorageWrapper(DataRootDirectory);

            var docs = wrapper.WebDB.GetContext().Documents
                .Where(x=>x.Url.Contains(pattern))
                .ToList();

            int i = 0;
            foreach(var doc in docs)
            {
                i++;
                Console.WriteLine($"{i} of {docs.Count}\tDeleting {doc.Url}");
                wrapper.RemoveResponse(doc.GeminiUrl);
                archiver.RemoveContent(doc.GeminiUrl);

                int fff = 65;
            }
        }

        static void ExcludeFilesFromArchive()
        {

            Archiver archiver = new Archiver(ArchiveDBPath, PacksPath);
            WebDatabaseContext context = new WebDatabaseContext(DataRootDirectory);

            int count = 0;
            foreach(var domain in context.Domains
                .Where(x=>x.HasRobotsTxt))
            {
                RobotsTxtFile robots = new RobotsTxtFile(domain.RobotsTxt);
                if(robots.IsMalformed)
                {
                    continue;
                }
                //we only care about Robots.txt files that have archiver rules.
                if(!robots.UserAgents.Contains("archiver"))
                {
                    continue;
                }

                //grab all the URLs for this domain and port
                foreach(var url in archiver.Context.Urls
                    .Where(x=>x.Domain == domain.DomainName && x.Port == domain.Port &&x.IsPublic))
                {
                    if (!robots.IsPathAllowed("archiver", url.GeminiUrl.Path))
                    {
                        count++;
                        Console.WriteLine($"{count}\tGoing to exclude {url.FullUrl}");
                        url.IsPublic = false;
                    }
                }
            }

            archiver.Context.SaveChanges();

            int x = 4;
        
        }


    }
}
