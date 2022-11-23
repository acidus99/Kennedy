using System;

using Gemini.Net;

namespace Kennedy.Blazer.Processors
{
	public interface IResponseProcessor
	{
		bool CanProcessResponse(GeminiResponse response);
		void ProcessResponse(GeminiResponse response);
    }
}

