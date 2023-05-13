//using System;
//using Microsoft.EntityFrameworkCore;

//using Kennedy.SearchIndex.Models;
//using Kennedy.SearchIndex.Web;

//namespace Kennedy.Crawler.Support
//{
//	public class GraphGenerator
//	{

//		WebDatabaseContext db;

//		StreamWriter fout;

//        public GraphGenerator(string storageDir)
//		{
//			db = new WebDatabaseContext(storageDir);
//		}

//		public void WriteGraph(string filename, string domain)
//		{
//			fout = new StreamWriter(filename);

//			fout.WriteLine("digraph SiteMap {");
//			fout.WriteLine(" rankdir=\"LR\"");
//			var docs = db.Documents.Where(x => x.Domain == domain).OrderBy(x=>x.Url.Length);

//            foreach (var doc in docs)
//            {
//                Uri url = new Uri(doc.Url);

//                var graphID = UrlIDToGraphID(doc.UrlID);

//				var label = url.PathAndQuery;
//				if(doc.Title?.Length >0)
//				{
//					label = doc.Title.Replace('\"', '\'') + "\n" + label;
//				}

//                fout.WriteLine($"{graphID} [label=\"{label}\"];");
//            }

//			var validInternalUrlIds = docs.Select(x => x.UrlID).Distinct();

//            foreach (var doc in docs)
//			{
//				var graphID = UrlIDToGraphID(doc.UrlID);

//				var links = db.Links
//					.Where(x => x.SourceUrlID == doc.UrlID && !x.IsExternal && validInternalUrlIds.Contains(x.TargetUrlID));

//                foreach (var link in links)
//				{
//					fout.WriteLine($"{graphID} -> {UrlIDToGraphID(link.TargetUrlID)};");
//				}
//			}
//            fout.WriteLine("}");
//			fout.Close();

//        }

//		private string UrlIDToGraphID(long urlID)
//			=> "ID_" + urlID.ToString().Replace('-', '_');

//	}
//}

