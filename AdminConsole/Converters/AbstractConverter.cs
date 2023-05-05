namespace Kennedy.AdminConsole.Converters;

using System;
using Gemini.Net;
using System.Net.Mime;
using Warc;
using Kennedy.AdminConsole.Db;

public abstract class AbstractConverter
{
    protected GeminiWarcCreator WarcCreator { get; private set; }

    public AbstractConverter(GeminiWarcCreator warcCreator)
	{
        WarcCreator = warcCreator;
	}

    public abstract void ToWarc();

    protected string GetJustMimetype(string meta)
    {

        if (meta.Length == 0)
        {
            return "text/gemini";
        }
        int paramIndex = meta.IndexOf(";");
        return  (paramIndex > 0) ?
                meta.Substring(0, paramIndex) :
                meta;
    }

    protected bool IsTruncated(SimpleDocument document)
    {
        if (document.BodySkipped)
        {
            return true;
        }

        if (document.Status == 20 && document.Meta.StartsWith("Requestor aborting due to reaching max download"))
        {
            return true;
        }
        return false;
    }

}

