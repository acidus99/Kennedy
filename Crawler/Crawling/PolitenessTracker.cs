using Gemini.Net;
namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Tracks timing delay to use between requests for a host
/// </summary>
public class PolitenessTracker
{
    /// <summary>
    /// how long should we wait between requests to the same authority
    /// </summary>
    const int DefaultDelayMs = 200;
    const int MaxDelayMs = 60000;

    Dictionary<string, int> DelayForAuthority = new Dictionary<string, int>();

    /// <summary>
    /// Increases the time used between requests for the same authority
    /// </summary>
    /// <param name="url"></param>
    public void IncreasePoliteness(GeminiUrl url)
    {
        string authority = url.Authority;

        if (!DelayForAuthority.ContainsKey(authority))
        {
            DelayForAuthority[authority] = DefaultDelayMs;
        }

        DelayForAuthority[authority] *= 2;

        if (DelayForAuthority[authority] > MaxDelayMs)
        {
            DelayForAuthority[authority] = MaxDelayMs;
        }
    }

    /// <summary>
    /// Gets the duration, in ms, to wait between requests to the same authority
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public int GetDelay(GeminiUrl url)
    {
        string authority = url.Authority;

        if(!DelayForAuthority.ContainsKey(authority))
        {
            return DefaultDelayMs;
        }

        return DelayForAuthority[authority];
    }
}
