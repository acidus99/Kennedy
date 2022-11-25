using System;
namespace Kennedy.Blazer.Logging
{
	public interface IStatusProvider
	{
		string ModuleName { get; }

		string GetStatus();
	}
}

