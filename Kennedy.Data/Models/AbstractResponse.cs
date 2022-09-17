using System;
namespace Kennedy.Data.Models
{
	public class AbstractResponse
	{
		public ContentType ContentType { get; internal set; } = ContentType.Unknown;

		public List<FoundLink> Links { get; set; } = new List<FoundLink>();
	}
}

