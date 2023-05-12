﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Table("Images")]
    public class Image
    {
        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        [Key]
        public long UrlID { get; set; }
        
        public int Width { get; set; }

        public int Height { get; set; }

        public string ImageType { get; set; }

        public bool IsTransparent { get; set; }

        public Image()
        { }

        public Image(GeminiUrl url)
        {
            UrlID = url.ID;
        }

    }
}
