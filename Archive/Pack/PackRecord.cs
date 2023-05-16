using System;
using System.Text;


namespace Kennedy.Archive.Pack
{
	public class PackRecord
	{
		public required string Type { get; set; }

		public required byte[] Data { get; set; }

		public string GetAsString()
			=> Encoding.UTF8.GetString(Data);
	}
}

