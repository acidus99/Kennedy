using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemini.Net;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive.Db;

/// <summary>
/// Represents a URL in the archive's URL registry.
/// The PK (<see cref="Id"/>) matches the <c>GeminiUrl.ID</c> computed by the Gemini.Net library,
/// enabling cross-reference without a separate lookup.
/// </summary>
[Table("Urls")]
[Index(nameof(Domain))]
[Index(nameof(Port))]
[Index(nameof(Protocol))]
public class Url
{
    /// <summary>PK — matches GeminiUrl.ID (a deterministic hash of the normalized URL string).</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>The normalized URL string.</summary>
    public string FullUrl { get; set; } = "";

    /// <summary>Hostname component of the URL.</summary>
    public string Domain { get; set; } = "";

    /// <summary>Scheme (always "gemini" in practice).</summary>
    public string Protocol { get; set; } = "";

    /// <summary>Port number (default 1965).</summary>
    public int Port { get; set; } = 1965;

    /// <summary>When false, the URL is excluded from public-facing archive views.</summary>
    public bool IsPublic { get; set; }

    public ICollection<Snapshot> Snapshots;

    /// <summary>Lazily-constructed <see cref="GeminiUrl"/> backed by <see cref="FullUrl"/>.</summary>
    [NotMapped]
    public GeminiUrl GeminiUrl
    {
        get
        {
            if (geminiUrl == null)
            {
                geminiUrl = new GeminiUrl(FullUrl);
            }
            return geminiUrl;
        }
    }

    private GeminiUrl? geminiUrl = null;

    public Url()
    {
        Snapshots = new List<Snapshot>();
    }

    public Url(GeminiUrl url)
    {
        Id = url.ID;
        FullUrl = url.NormalizedUrl;
        geminiUrl = url;
        Domain = url.Hostname;
        Port = url.Port;
        Protocol = url.Protocol;

        Snapshots = new List<Snapshot>();
    }
}

