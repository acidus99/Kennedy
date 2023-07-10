using System;
using System.Text.RegularExpressions;

using Gemini.Net;


namespace Kennedy.Crawler.Filters
{
	public class BlockRule
	{
		public bool IsGlobalRule
			=> (Scope == "");

		public string Definition { get; private set; }

		public string Scope { get; private set; }

		private readonly Regex? _regex;

		private readonly RuleType _type;

		private readonly string? _prefix;

		public BlockRule(string ruleDefinition)
		{
			Definition = ruleDefinition;

			//is it a regex rule (
			if (ruleDefinition.StartsWith("regex:") && ruleDefinition.Length >= 7)
			{
				_type = RuleType.Regex;
				_regex = new Regex(ruleDefinition.Substring(6).Trim());
				Scope = "";
				return;
			}
			// is it a global rule (ends in a *)
			else if (ruleDefinition.EndsWith("*"))
			{
				_type = RuleType.Prefix;
				_prefix = ruleDefinition.Replace("*", "");
				Scope = "";
				return;
            }
			//a prefix rule, not allowing subs
			else if(ruleDefinition.EndsWith("$"))
			{
				_type = RuleType.NoSubFiles;
				_prefix = ruleDefinition.Replace("$", "");
				var parsedScope = GetScope(_prefix);
				if(parsedScope == null)
				{
					throw new ArgumentException("Block rule not a valid Gemini URL", nameof(ruleDefinition));
				}
				Scope = parsedScope;
				return;
            }
			//general prefix rule
			else
			{
                _type = RuleType.Prefix;
				_prefix = ruleDefinition;
                var parsedScope = GetScope(_prefix);
                if (parsedScope == null)
                {
                    throw new ArgumentException("Block rule not a valid Gemini URL", nameof(ruleDefinition));
                }
                Scope = parsedScope;
            }
        }

		private string? GetScope(string rule)
		{
			GeminiUrl? url = GeminiUrl.MakeUrl(rule);
			return url?.Authority;
		}

		public bool IsMatch(GeminiUrl url)
		{
			switch(_type)
			{
				case RuleType.Prefix:
					return IsMatchPrefix(url);

				case RuleType.NoSubFiles:
					return IsMatchSubFiles(url);

				case RuleType.Regex:
					return IsMatchRegex(url);
			}
			throw new ArgumentException("Unknown type in block rule");
		}

		private bool IsMatchPrefix(GeminiUrl url)
			=> url.NormalizedUrl.StartsWith(_prefix!);

        private bool IsMatchSubFiles(GeminiUrl url)
			=> url.NormalizedUrl.StartsWith(_prefix!) && url.NormalizedUrl != _prefix;

        private bool IsMatchRegex(GeminiUrl url)
			=> _regex!.IsMatch(url.NormalizedUrl);

        private enum RuleType
		{
			Prefix,
			NoSubFiles,
			Regex,
		}
	}
}

