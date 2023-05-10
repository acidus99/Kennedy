using System;
using System.Text;
using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;

namespace Kennedy.Archive
{
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
			var bytes = ReadBytes(snapshot);
			return GeminiParser.ParseResponseBytes(snapshot.Url.GeminiUrl, bytes);
        }

        public byte[] ReadBytes(Snapshot snapshot)
        {
            var record = GetRecord(snapshot);
            return (record != null) ? ReadPackData(record) :
                null;
        }

        private PackRecord GetRecord(Snapshot snapshot)
		{
			if(snapshot.Offset == null)
			{
				throw new NullReferenceException("Snapshot doesn't have an Offset value");
			}

            var pack = manager.GetPack(snapshot.Url.PackName);
            return pack.Read(snapshot.Offset.Value);
        }

		private byte[] ReadPackData(PackRecord record)
			=> (record.Type == "DATZ") ?
				GzipUtils.Decompress(record.Data) :
				record.Data;
    }
}
