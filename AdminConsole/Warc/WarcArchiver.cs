//using System;
//using System.Threading.Tasks;
//using System.Linq;
//using Toimik.WarcProtocol;

//using Gemini.Net;
//using Kennedy.Archive;


//namespace Kennedy.AdminConsole.Warc
//{
//    static class WarcArchiver
//    {
//        static int validResponses = 0;
//        static int tooBig = 0;
//        static int ableToArchive = 0;
//        static int invalidResponses = 0;
//        static int totalResponses = 0;
//        static int totalRecords = 0;

//        static StreamWriter fout = new StreamWriter("/tmp/warc.txt");

//        static Archiver Archiver = new Archiver("/Users/billy/kennedy-capsule/crawl-data/archive.db", "/Users/billy/kennedy-capsule/crawl-data/Packs/");

//        static async Task Main(string[] args)
//        {
//            int counter = 0;

//            var files = Directory.GetFiles("/Users/billy/tmp/mozz-archive/", "*.warc");
//            int fileCount = 0;
//            foreach (var warcFile in files)
//            {
//                fileCount++;
//                var parser = new WarcParser();

//                // Parse the file and process the records accordingly
//                var records = parser.Parse(warcFile);


//                await foreach (Record record in records)
//                {
//                    counter++;
//                    if (counter % 100 == 0)
//                    {
//                        Console.WriteLine($"File {fileCount} of {files.Length}:\t\t{counter} - {totalResponses} - {ableToArchive} - BIG: {tooBig}");
//                    }
//                    totalRecords++;
//                    switch (record.Type)
//                    {
//                        case ResponseRecord.TypeName:
//                            HandleResponse(record as ResponseRecord);
//                            break;
//                    }
//                }
//            }
//            fout.Close();
//            Console.WriteLine($"Done");
//            Console.WriteLine($"Records:\t{totalRecords}.");
//            Console.WriteLine($"Gemini Responses:\t{totalResponses}");
//            Console.WriteLine($"Invalid Responses:\t{invalidResponses}");
//            Console.WriteLine($"Valid Responses:\t{validResponses}");
//            Console.WriteLine($"Archivable:\t{ableToArchive}");
//            Console.WriteLine($"Too Big:\t{tooBig}.");
//        }

//        static void HandleResponse(ResponseRecord response)
//        {
//            GeminiUrl url = new GeminiUrl(response.TargetUri);

//            if (response.ContentBlock != null)
//            {
//                totalResponses++;
//                GeminiResponse resp = null;

//                try
//                {
//                    resp = GeminiParser.ParseBytes(url, response.ContentBlock);
//                    if (ArchiveResponse(response.Date, resp))
//                    {
//                        ableToArchive++;
//                    }
//                    validResponses++;
//                }
//                catch (Exception)
//                {
//                    invalidResponses++;
//                }
//            }
//        }

//        static bool ArchiveResponse(DateTime captured, GeminiResponse response)
//        {
//            if (response.IsSuccess && response.HasBody && response.BodySize < 30000000)
//            {
//                //store the whole thing!
//                return Archiver.ArchiveResponse(captured, response.RequestUrl, response.StatusCode, response.Meta, response.BodyBytes);
//            }
//            else if (response.IsInput || response.IsRedirect || response.IsAuth)
//            {
//                return Archiver.ArchiveResponse(captured, response.RequestUrl, response.StatusCode, response.Meta);
//            }
//            return false;
//        }

//    }
//}
