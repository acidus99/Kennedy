using System.IO;

namespace Kennedy.Server;

public class Settings
{
    public static Settings Global = null!;

    public required string Host { get; set; }
    public required int Port { get; set; }
    public required string CertificateFile { get; set; }
    public required string KeyFile { get; set; }
    public required string PublicRoot { get; set; }
    public required string DataRoot { get; set; }

    public string ArchiveStatsFile
        => DataRoot + "archive-stats.json";

    public string ArchiveDatabaseFile
        => Path.Combine(DataRoot, "archive.db");

    public string SearchStatsFile
        => DataRoot + "search-stats.json";

    public string SearchDbFile
        => Path.Combine(DataRoot, "kennedy2.db");
}
