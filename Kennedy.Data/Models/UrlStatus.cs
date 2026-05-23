namespace Kennedy.Data.Models;

public enum UrlStatus
{
    /// <summary>
    /// Discovered but never fetched.
    /// </summary>
    New = 0,

    /// <summary>
    /// Last meaningful visit returned indexable content
    /// (e.g. Gemini 20) and no policy prevents indexing.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Retryable protocol-level error (4x / 5xx / timeouts, etc.).
    /// </summary>
    TemporaryError = 2,

    /// <summary>
    /// Low-level network / TLS / DNS issues (no protocol status).
    /// </summary>
    ConnectionError = 3,

    /// <summary>
    /// Non-retryable failure (malformed URL, repeated 50s, etc.).
    /// </summary>
    PermanentError = 4,

    /// <summary>
    /// URL is definitely gone (e.g. 404 / 410 / Gemini 51).
    /// </summary>
    Gone = 5,

    /// <summary>
    /// URL only ever redirects (30/31, 301/302, etc.).
    /// </summary>
    Redirect = 6,

    /// <summary>
    /// Blocked by robots rules.
    /// </summary>
    ExcludedByRobots = 7,

    /// <summary>
    /// Explicitly deny-listed by operator or rules.
    /// </summary>
    DenyList = 8,

    /// <summary>
    /// Automatically suppressed as a low-value / highly templated URL.
    /// </summary>
    LowValueSuppressed = 9,

    /// <summary>
    /// Removed from public surfaces due to owner request.
    /// </summary>
    RemovedByOwnerRequest = 10,

    /// <summary>
    /// Manually disabled by operator for any other reason.
    /// </summary>
    ManuallyDisabled = 11,

    Interactive = 12,

    UNKNOWN= 99,
}
