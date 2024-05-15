namespace Kennedy.Warc;

public class MimeStat
{
    public required string MimeType { get; set; }
    public long TotalSize { get; set; } = 0;
    public long Count { get; set; } = 0;
    public long Savings { get; set; } = 0;
}