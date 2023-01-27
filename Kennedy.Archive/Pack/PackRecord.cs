using System;
using System.Text;


namespace Kennedy.Archive.Pack
{
	public class PackRecord
	{
		public string Type;

		public long Length;

		public byte[] Data;

		public string GetAsString()
			=> Encoding.UTF8.GetString(Data);
	}
}

