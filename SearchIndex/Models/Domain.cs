﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemini.Net;


namespace Kennedy.SearchIndex.Models
{
    [Table("Domains")]
    public class Domain
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("Domain")]
        public string DomainName { get; set; }
        public int Port { get; set; }

        public bool IsReachable { get; set; }
        public string ErrorMessage { get; set; }

        public bool HasRobotsTxt { get; set; }
        public bool HasFaviconTxt { get; set; }
        public bool HasSecurityTxt { get; set; }
        
        public string FaviconTxt { get; set; }
        public string SecurityTxt { get; set; }
        public string RobotsTxt { get; set; }
    }
}