namespace Kennedy.Archive.Pack;

public static class PackRecordFactory
{
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


    public static PackRecord MakeOptimalRecord(byte[] data)
    {
        byte[] compressed = GzipUtils.Compress(data);
        //if we reduced file size by at least 10%, than use the smaller one
        if (compressed.Length < data.Length * 0.9)
        {
            return MakeDatzRecord(compressed);
        }
        else
        {
            return MakeDataRecord(data);
        }
    }

    public static PackRecord MakeInfoRecord(string text)
        => MakeRecord("INFO", text);

    public static PackRecord MakeDataRecord(byte[] data)
        => MakeRecord("DATA", data);

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
