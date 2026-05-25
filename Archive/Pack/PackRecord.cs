namespace Kennedy.Archive.Pack;

/// <summary>
/// An in-memory representation of one record in a pack file.
/// On disk a record is: 4-byte ASCII type tag + 4-byte uint32 length + N data bytes.
/// </summary>
public class PackRecord
{
    /// <summary>
    /// 4-character ASCII type identifier (space-padded if shorter).
    /// Known types: <c>"DATA"</c> (raw bytes), <c>"DATZ"</c> (gzip-compressed bytes), <c>"INFO"</c> (UTF-8 text).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>The record payload. For DATZ records these are already compressed; callers must decompress.</summary>
    public required byte[] Data { get; set; }
}
