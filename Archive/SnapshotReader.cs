﻿using Gemini.Net;
using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;

namespace Kennedy.Archive;

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

    public GeminiResponse ReadResponse(Snapshot snapshot)
    {
        if (snapshot.Url == null)
        {
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot have a null Url property");
        }

        byte[] bytes = ReadBytes(snapshot);
        return GeminiParser.ParseResponseBytes(snapshot.Url.GeminiUrl, bytes);
    }

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
