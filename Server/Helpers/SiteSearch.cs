using System.Text.RegularExpressions;

namespace Kennedy.Server.Helpers
{
	public static class SiteSearch
	{
        static readonly Regex findCapsule = new Regex(@"\/sitesearch\/([a-z0-9\-\.\:]+)\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex validCapsule = new Regex(@"^[a-z0-9\.\-]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string? GetSite(string route)
		{
			var match = findCapsule.Match(route);
			if(match.Success)
			{
				return match.Groups[1].Value;
			}
			return null;
		}

		public static bool IsValidCapsuleName(string capsule)
			=> validCapsule.IsMatch(capsule);
	}
}

