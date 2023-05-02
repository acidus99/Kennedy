using System;
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
        public string? ErrorMessage { get; set; }

        [NotMapped]
        public bool HasSecurityTxt
            => (SecurityUrlID != null);

        [NotMapped]
        public bool HasRobotsTxt
            => (RobotsUrlID != null);

        public long? RobotsUrlID { get; set; }
        public long? FaviconUrlID { get; set; }
        public long? SecurityUrlID { get; set; }
        
        public string? FaviconTxt { get; set; }
    }
}
