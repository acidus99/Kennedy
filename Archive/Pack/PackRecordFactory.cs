namespace Kennedy.Archive.Pack;

/// <summary>
/// Creates and serializes <see cref="PackRecord"/> instances.
/// <see cref="MakeOptimalRecord"/> is the primary entry point: it compresses the data with gzip
/// and uses the compressed form only if it achieves at least 10% size reduction.
/// </summary>
public static class PackRecordFactory
{
    /// <summary>Serializes a <see cref="PackRecord"/> to its on-disk binary format: 4-byte type + 4-byte length + data.</summary>
    public static byte[] ToBytes(PackRecord record)
    {
        List<byte> buffer = new List<byte>();
        buffer.AddRange(MakeType(record.Type));
        buffer.AddRange(ConvertLength(record.Data.Length));
        buffer.AddRange(record.Data);
        return buffer.ToArray();
    }

    private static byte[] ConvertLength(int length)
        => BitConverter.GetBytes((uint)length);

    private static byte[] MakeType(String type)
    {
        if (type.Length > 4)
        {
            throw new ArgumentException("type is more than 4 characters");
        }
        while (type.Length < 4)
        {
            type += " ";
        }
        return System.Text.Encoding.ASCII.GetBytes(type);
    }


    /// <summary>
    /// Creates the most space-efficient record for <paramref name="data"/>.
    /// Compresses with gzip; if the compressed form is at least 10% smaller, returns a DATZ record.
    /// Otherwise returns a DATA record with the original bytes.
    /// </summary>
    public static PackRecord MakeOptimalRecord(byte[] data)
    {
        byte[] compressed = GzipUtils.Compress(data);
        if (compressed.Length < data.Length * 0.9)
        {
            return MakeDatzRecord(compressed);
        }
        else
        {
            return MakeDataRecord(data);
        }
    }

    /// <summary>Creates an INFO record containing UTF-8 encoded text metadata.</summary>
    public static PackRecord MakeInfoRecord(string text)
        => MakeRecord("INFO", text);

    /// <summary>Creates a DATA record containing raw (uncompressed) bytes.</summary>
    public static PackRecord MakeDataRecord(byte[] data)
        => MakeRecord("DATA", data);

    /// <summary>Creates a DATZ record containing already-compressed bytes. The caller is responsible for compressing first.</summary>
    public static PackRecord MakeDatzRecord(byte[] data)
        => MakeRecord("DATZ", data);

    private static PackRecord MakeRecord(string type, string text)
        => MakeRecord(type, System.Text.Encoding.UTF8.GetBytes(text));

    private static PackRecord MakeRecord(string type, byte[] data)
        => new PackRecord
        {
            Type = type,
            Data = data
        };
}
