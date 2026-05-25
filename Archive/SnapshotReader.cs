using Gemini.Net;
using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;

namespace Kennedy.Archive;

/// <summary>
/// Reads archived Gemini responses from the pack file storage layer.
/// Given a <see cref="Snapshot"/> row (which carries a content hash and byte offset),
/// retrieves the correct pack file, seeks to the offset, decompresses if necessary,
/// and deserializes the bytes back into a <see cref="GeminiResponse"/>.
/// </summary>
public class SnapshotReader
{
    PackManager manager;

    public SnapshotReader(string packLocation)
        : this(new PackManager(packLocation))
    {
    }

    public SnapshotReader(PackManager packManager)
    {
        manager = packManager;
    }

    /// <summary>
    /// Reads the response bytes for <paramref name="snapshot"/> from disk and deserializes them
    /// into a <see cref="GeminiResponse"/>.
    /// Requires <c>snapshot.Url</c> to be non-null (loaded with <c>.Include(s =&gt; s.Url)</c>).
    /// </summary>
    public GeminiResponse ReadResponse(Snapshot snapshot)
    {
        if (snapshot.Url == null)
        {
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot have a null Url property");
        }

        byte[] bytes = ReadBytes(snapshot);
        return GeminiParser.ParseResponseBytes(snapshot.Url.GeminiUrl, bytes);
    }

    /// <summary>
    /// Returns the raw (decompressed) response bytes for <paramref name="snapshot"/>.
    /// Use this when you need the bytes rather than a parsed GeminiResponse.
    /// </summary>
    public byte[] ReadBytes(Snapshot snapshot)
    {
        var record = GetRecord(snapshot);
        return ReadPackData(record);
    }

    private PackRecord GetRecord(Snapshot snapshot)
    {
        if (snapshot.Url == null)
        {
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot have a null Url property");
        }

        var pack = manager.GetPack(snapshot.DataHash);
        return pack.Read(snapshot.Offset);
    }

    private byte[] ReadPackData(PackRecord record)
        => (record.Type == "DATZ") ?
            GzipUtils.Decompress(record.Data) :
            record.Data;
}
