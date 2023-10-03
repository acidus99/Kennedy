namespace Kennedy.WarcConverters.MozzPortalImport;

using System.Security.Cryptography.X509Certificates;
using Gemini.Net;

public class ArchivedContent
{
	public required WaybackUrl Url { get; init; }

	public GeminiResponse? GeminiResponse { get; set; }

	public X509Certificate2? Certificate { get; set; }
}

