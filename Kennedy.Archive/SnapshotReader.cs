using System;
using System.Text;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;

namespace Kennedy.Archive
{
	public class SnapshotReader
	{
		PackManager manager = new PackManager("/Users/billy/Desktop/Packs");

        public SnapshotReader()
		{

		}

		public string ReadText(Snapshot snapshot)
		{

			var record = GetRecord(snapshot);

			return (record != null) ?
				Encoding.UTF8.GetString(ReadPackData(record)) :
				"";
        }


		private PackRecord GetRecord(Snapshot snapshot)
		{
            var pack = manager.GetPack(snapshot.Url.PackName);
            return pack.Read(snapshot.Offset);
        }


		private byte[] ReadPackData(PackRecord record)
			=> (record.Type == "DATZ") ?
				GzipUtils.Decompress(record.Data) :
				record.Data;
    }
}

