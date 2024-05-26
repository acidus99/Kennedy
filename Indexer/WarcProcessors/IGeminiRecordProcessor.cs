using Gemini.Net;

namespace Kennedy.Indexer.WarcProcessors;

public interface IGeminiRecordProcessor
{
    public void ProcessGeminiResponse(GeminiResponse response);

    public void FinalizeProcessing();
}