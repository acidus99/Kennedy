namespace Kennedy.Archive.Pack;

public class PackFile
{
    string FullPath;
    string Path;

    public PackFile(string path, string filename)
    {
        Path = path;
        FullPath = Path + filename;
    }

    public long Append(PackRecord packRecord)
    {
        var offset = GetOffset();
        //ensure the path exists
        Directory.CreateDirectory(Path);
        using (var fout = new FileStream(FullPath, FileMode.Append))
        {
            var data = PackRecordFactory.ToBytes(packRecord);
            fout.Write(data);
        }
        return offset;
    }

    public PackRecord Read(long offset)
    {
        using (var fin = new BinaryReader(new FileStream(FullPath, FileMode.Open)))
        {
            fin.BaseStream.Seek(offset, SeekOrigin.Begin);
            string type = GetType(fin.ReadBytes(4));
            long len = Convert.ToInt64(fin.ReadUInt32());

            var data = fin.ReadBytes((int)len);
            return new PackRecord
            {
                Type = type,
                Data = data
            };
        }
    }

    private long GetOffset()
    {
        try
        {
            return (new FileInfo(FullPath)).Length;
        }
        catch (Exception)
        {
        }
        //file doesn't exist, so the offset is zero
        return 0;
    }

    private string GetType(byte[] type)
        => System.Text.Encoding.ASCII.GetString(type);
}

