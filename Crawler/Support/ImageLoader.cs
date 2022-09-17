using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ImageMagick;
using Microsoft.Data.Sqlite;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;

namespace Kennedy.Crawler.Support
{
    public class ImageLoader
    {
        DocumentStore docStore = new DocumentStore(CrawlerOptions.DataDirectory + "page-store/");

        DocIndexDbContext db = new DocIndexDbContext(CrawlerOptions.DataDirectory);

        public void LoadImages()
        {

            var tmp = db.ImageEntries.FirstOrDefault();

            var images = db.DocEntries.Where(x => (x.ErrorCount == 0 && x.Status == 20 && x.BodySaved == true && x.ContentType == ContentType.Image));

            var total = images.Count();
            int counter = 0;
            int hits = 0;
            foreach (var image in images)
            {

                counter++;
                if (counter % 10 == 0)
                {
                    Console.WriteLine($"{counter}\t{total}\t{hits}");
                }

                image.SetDocID();
                var imageData = LoadImage(image.DocID);

                if(imageData != null)
                {

                    StoredImageEntry imageEntry = new StoredImageEntry
                    {
                        DocID = image.DocID,
                        DBDocID = image.DBDocID,
                        IsTransparent = !imageData.IsOpaque,
                        Height = imageData.Height,
                        Width = imageData.Width,
                        ImageType = imageData.Format.ToString()
                    };
                    hits++;
                    db.ImageEntries.Add(imageEntry);
                }

            }
            db.SaveChanges();

        }

        public MagickImage LoadImage(ulong docID)
        {

            var bytes = docStore.GetDocument(docID);

            if (bytes == null)
            {
                return null;
            }
            try
            {
                return new MagickImage(bytes);
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }

}
