namespace Kennedy.WarcConverters.MozzPortalImport;

using System.Text;
using System.Web;

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Gemini.Net;

public class HtmlReverser
{
	IElement Root;
	StringBuilder Buffer;

	WaybackUrl WaybackUrl;

	GeminiUrl Origin
		=> WaybackUrl.GetProxiedUrl();

	public HtmlReverser(WaybackUrl waybackUrl, IElement root)
	{
		WaybackUrl = waybackUrl;
		Root = root;
		Buffer = new StringBuilder();
	}

	public string ReverseToGemtext()
	{
		ConvertChildren(Root);
		return Buffer.ToString();
	}

	private void ConvertChildren(IElement element)
	{
		foreach(var child in element.ChildNodes)
		{
			if (child.NodeType != NodeType.Element && child.TextContent.Trim().Length > 0)
			{
				throw new ApplicationException("Got a non empty, non element, node");
			}

			if(child is not IElement)
			{
				continue;
			}

			IElement childElement = (IElement)child;

			Convert(childElement);
		}
	}

        private void Convert(IElement element)
        {
		string tag = element.TagName.ToLower();

		switch(tag) 
		{
			case "a":
				ConvertAnchor((IHtmlAnchorElement)element);
				break;

			case "blockquote":
				ConvertBlockquote(element);
				break;

                case "br":
				Buffer.AppendLine();
				break;

			case "h1":
				Buffer.AppendLine($"# {FormatText(element.TextContent)}");
				break;

                case "h2":
                    Buffer.AppendLine($"## {FormatText(element.TextContent)}");
                    break;

                case "h3":
                    Buffer.AppendLine($"### {FormatText(element.TextContent)}");
                    break;

			case "li":
                    if (element.TextContent.Contains("\n"))
                    {
                        throw new ApplicationException("LI contains \\n");
                    }
                    Buffer.AppendLine($"* {FormatText(element.TextContent)}");
				break;


                case "p":
                    Buffer.Append(FormatText(element.TextContent));
                    break;

                case "pre":
				Buffer.AppendLine("```");
				Buffer.AppendLine(element.TextContent);
                    Buffer.AppendLine("```");
				break;

			case "ul":
				ConvertChildren(element);
				break;

                default:
				throw new ApplicationException($"Unknown Tag '{tag}'");
		}
        }

	private void ConvertAnchor(IHtmlAnchorElement anchor)
	{
		if (anchor.NextElementSibling != null)
		{
			bool deleted = false;
			bool checkAgain;
			do
			{
				checkAgain = false;
				string nextTag = anchor.NextElementSibling.TagName.ToLower();
				if (nextTag != "br" && nextTag != "span")
				{
					throw new ApplicationException("Anchor tag not followed by BR or SPAN tag!");
				}
				if(nextTag == "span")
				{
					anchor.NextElementSibling.Remove();
					if(deleted)
					{
						throw new ApplicationException("Deleting two spans after an anchor!!!");
					}
					deleted = true;
					checkAgain = true;
				}
			} while (checkAgain);
		}

		string href = GetLink(anchor.HyperReference(anchor.Href));
        
		Buffer.Append($"=> {href} {anchor.TextContent}");
        }

	private void ConvertBlockquote(IElement element)
	{
		//Mozz's blockquotes can have new lines, so split them and do multiple gemtext blockquote lines
		foreach (var line in element.TextContent.Split('\n'))
		{
			Buffer.AppendLine($"> {FormatText(line)}");
		}
	}

	private string FormatText(string s)
		=> HttpUtility.HtmlDecode(s);

	private string GetLink(Uri url)
	{
		//create a wayback link from the linkTarget
		WaybackUrl targetLink = new WaybackUrl(url);

		//if not to mozz, just return the source
		if (!targetLink.IsMozzUrl)
		{
			return targetLink.SourceUrl.AbsoluteUri;
		}

		var geminiTarget = targetLink.GetProxiedUrl();

		string href = geminiTarget.ToString();

		//is it to the same as the origin?
		if (geminiTarget.Authority == Origin.Authority)
		{
			href = geminiTarget.Url.PathAndQuery;
		}
		return href;
	}
}

