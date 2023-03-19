using System;
using System.Text;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;

namespace Kennedy.Archive
{
	public class SnapshotReader
	{
		PackManager manager;

        public SnapshotReader(string packsLocation)
		{
			manager = new PackManager(packsLocation);
		}

		public string ReadText(Snapshot snapshot)
		{

			var bytes = ReadBytes(snapshot);

			return (bytes != null) ?
				Encoding.UTF8.GetString(bytes) :
				null;
        }


        public byte[] ReadBytes(Snapshot snapshot)
        {
            var record = GetRecord(snapshot);
            return (record != null) ? ReadPackData(record) :
                null;
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

