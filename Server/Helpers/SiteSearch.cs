using System.Text.RegularExpressions;

namespace Kennedy.Server.Helpers
{
	public static class SiteSearch
	{
        static readonly Regex findCapsule
			= new Regex( RoutePaths.SiteSearchRunRoute.Replace("/",@"\/")
			+ @"([^\/]+)\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex validCapsule = new Regex(@"^[a-z0-9\.\-\:]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string? GetSite(string route)
		{
			var match = findCapsule.Match(route);
			if(match.Success)
			{
				var capsuleName = match.Groups[1].Value;
				if(IsValidCapsuleName(capsuleName))
				{
					return capsuleName;
				}
			}
			return null;
		}

		public static bool IsValidCapsuleName(string capsule)
			=> validCapsule.IsMatch(capsule) && capsule.Contains('.');
	}
}

