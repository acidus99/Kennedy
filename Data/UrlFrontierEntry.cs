using Gemini.Net;

namespace Kennedy.Data;

public class UrlFrontierEntry
{
    public required GeminiUrl Url { get; set; }

    public bool IsRobotsLimited { get; set; } = false;

    public int DepthFromSeed { get; set; } = 0;

    public bool IsProactive { get; set; } = false;

    public bool IsRetry
        => RetryCount > 0;

    public int RetryCount { get; set; } = 0;
}