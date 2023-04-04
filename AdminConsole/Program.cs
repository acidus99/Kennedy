using System.Linq;

using HashDepot;

using Gemini.Net;

using Kennedy.Archive;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Db;
using Kennedy.Data;

using Microsoft.EntityFrameworkCore;


namespace ArchiveLoader
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

                default:
                    Console.WriteLine($"Unknown operation '{Operation}'");
                    return false;

            }
        }

        static void AddCrawlToArchive(string crawlLocation)
        {
            var archiver = new Archiver(ArchiveDBPath, PacksPath);
            ModernImporter importer = new ModernImporter(archiver, crawlLocation);
            importer.Import();
        }

        static void DeleteFromCrawl(string url)
        {
            DocIndexDbContext index = new DocIndexDbContext(DataRootDirectory);
            GeminiUrl gurl = new GeminiUrl(url);
            index.DocEntries.Where(x => x.UrlID == gurl.ID).FirstOrDefault();




        }

    }
}
