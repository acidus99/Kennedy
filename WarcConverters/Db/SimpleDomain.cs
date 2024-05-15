using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.WarcConverters.Db;

[Table("Domains")]
public class SimpleDomain
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("Domain")]
    public required string Domain { get; set; }
    public required int Port { get; set; }

    public required bool IsReachable { get; set; }

    public required bool HasRobotsTxt { get; set; }
    public required bool HasFaviconTxt { get; set; }
    public required bool HasSecurityTxt { get; set; }

    public string? FaviconTxt { get; set; }
    public string? SecurityTxt { get; set; }
    public string? RobotsTxt { get; set; }
}