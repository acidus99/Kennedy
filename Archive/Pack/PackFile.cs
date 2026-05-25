namespace Kennedy.Archive.Pack;

/// <summary>
/// Reads and appends records to a single binary pack file on disk.
/// A pack file is an append-only sequence of <see cref="PackRecord"/> entries; each entry's
/// byte offset within the file is used as the retrieval key stored in <see cref="Db.Snapshot.Offset"/>.
/// The file is created on first write; the directory is created automatically.
/// </summary>
public class PackFile
{
    string FullPath;
    string Path;

    public PackFile(string path, string filename)
    {
        Path = path;
        FullPath = Path + filename;
    }

    /// <summary>
    /// Appends <paramref name="packRecord"/> to the end of the file.
    /// Returns the byte offset at which the record was written (i.e. the file length before the write),
    /// which should be stored in the corresponding <see cref="Db.Snapshot"/> for later retrieval.
    /// </summary>
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

    /// <summary>
    /// Reads the record stored at <paramref name="offset"/> bytes from the start of the file.
    /// The file must exist; use the offset returned by <see cref="Append"/> to locate a specific record.
    /// </summary>
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

