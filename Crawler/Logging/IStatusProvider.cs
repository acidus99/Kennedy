using System;
namespace Kennedy.Crawler.Logging
{
	public interface IStatusProvider
	{
		string ModuleName { get; }

		string GetStatus();
	}
}

