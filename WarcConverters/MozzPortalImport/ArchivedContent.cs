namespace Kennedy.WarcConverters.MozzPortalImport;

using System.Security.Cryptography.X509Certificates;
using Gemini.Net;

public class ArchivedContent
{
	/// <summary>
	/// The URL we downloaded and parsed from the Wayback machine
	/// </summary>
	public required WaybackUrl Url { get; init; }

	/// <summary>
	/// An extracted, parsed GeminiResponse, representing the content from the Wayback machine, if any
	/// </summary>
	public GeminiResponse? GeminiResponse { get; set; }

	/// <summary>
	/// The X509 Certificate that was stored for a capsule
	/// </summary>
	public X509Certificate2? Certificate { get; set; }

	/// <summary>
	/// Any additional URLs we should try
	/// </summary>
	public List<WaybackUrl> MoreUrls { get; } = new List<WaybackUrl>();
}

