using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemini.Net;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.AdminConsole.Importers
{
    [Table("Domains")]
    public class SimpleDomain
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("Domain")]
        public string Domain { get; set; }
        public int Port { get; set; }

        public bool IsReachable { get; set; }

        public bool HasRobotsTxt { get; set; }
        public bool HasFaviconTxt { get; set; }
        public bool HasSecurityTxt { get; set; }

        public string? FaviconTxt { get; set; }
        public string? SecurityTxt { get; set; }
        public string? RobotsTxt { get; set; }

    }

}
