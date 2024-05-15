using Gemini.Net;
using WarcDotNet;

namespace Kennedy.Warc;

/// <summary>
/// One off class to cleaning WARCs
/// </summary>
public static class WarcCleaner
{
    /// <summary>
    /// Fix TLS info that is on 49 responses (since the vast majority they didn't get TLS completed, and are using the value
    /// in the requestor from the previous response. Also, metadata records need to be removed too
    /// </summary>
    /// <param name="inputWarc"></param>
    /// <param name="outputWarc"></param>
    public static void FixTls(string inputWarc, string outputWarc)
    {
        DateTime prev = DateTime.Now;
        int Processed = 0;

        Dictionary<string, bool> Authorities = new Dictionary<string, bool>();

        //first read through and find the authority for all capsules that had a "49" status 
        using (WarcReader reader = new WarcReader(inputWarc))
        {
            foreach (var record in reader)
            {
                if (record is ResponseRecord responseRecord && record.ContentBlock != null && responseRecord.TargetUri != null)
                {
                    GeminiUrl url = new GeminiUrl(responseRecord.TargetUri);

                    GeminiResponse geminiResponse = GeminiParser.ParseResponseBytes(url, record.ContentBlock);
                    if (geminiResponse.StatusCode == 49)
                    {
                        Authorities[url.Authority] = true;
                    }
                }
            }
        }

        int cleaned = 0;
        int noworkneeded = 0;
        int metadata = 0;

        using (WarcWriter writer = new WarcWriter(outputWarc))
        {
            using (WarcReader reader = new WarcReader(inputWarc))
            {
                foreach (var record in reader)
                {
                    bool shouldWrite = true;
                    Processed++;

                    if (record is WarcInfoRecord infoRecord)
                    {
                        //Clear any block digest from a warcinfo
                        infoRecord.BlockDigest = null;
                    }

                    if (record is RequestRecord requestRecord && requestRecord.TargetUri != null)
                    {

                        //Clear any payload digest from a request
                        requestRecord.PayloadDigest = null;

                        //Fix the TLS stuff
                        FixWarcProtocol(record);
                        FixTlsCipherSuite(record);

                        //Check if this is something where we should remove the TLS info altogether
                        GeminiUrl url = new GeminiUrl(requestRecord.TargetUri);
                        if (Authorities.ContainsKey(url.Authority))
                        {
                            if (record.CustomFields.Count > 0)
                            {
                                cleaned++;
                                RemoveAllTlsFields(record);
                            }
                            else
                            {
                                noworkneeded++;
                            }
                        }
                    }

                    if (record is ResponseRecord responseRecord && responseRecord.TargetUri != null)
                    {
                        //Fix the TLS stuff
                        FixWarcProtocol(record);
                        FixTlsCipherSuite(record);

                        //Check if this is something where we should remove the TLS info altogether
                        GeminiUrl url = new GeminiUrl(responseRecord.TargetUri);
                        if (Authorities.ContainsKey(url.Authority))
                        {
                            if (record.CustomFields.Count > 0)
                            {
                                cleaned++;
                                RemoveAllTlsFields(record);
                            }
                            else
                            {
                                noworkneeded++;
                            }
                        }
                    }

                    if (record is MetadataRecord metadataRecord && metadataRecord.TargetUri != null)
                    {
                        GeminiUrl url = new GeminiUrl(metadataRecord.TargetUri);
                        if (Authorities.ContainsKey(url.Authority))
                        {
                            //don't write out incorrect certificate record
                            shouldWrite = false;
                            metadata++;
                        }
                    }
                    if (shouldWrite)
                    {
                        writer.Write(record);
                    }
                }
            }
        }

        int seconds = (int)DateTime.Now.Subtract(prev).TotalSeconds;
        if (seconds == 0)
        {
            seconds = 1;
        }

        Console.WriteLine($"Records: {Processed}\tTime: {seconds}s\tRate: {Processed / seconds} / s");
    }

    private static void RemoveAllTlsFields(WarcRecord record)
    {
        //remove incorrect TLS fields
        record.CustomFields.RemoveAll(GeminiWarcCreator.WarcProtocolField);
        record.CustomFields.RemoveAll(GeminiWarcCreator.WarcCipherSuiteField);
        //even the prototype field name
        record.CustomFields.RemoveAll("WARC-TLS-Cipher-Suite");
    }

    private static void FixWarcProtocol(WarcRecord record)
    {
        if (record.CustomFields.FieldCount(GeminiWarcCreator.WarcProtocolField) == 1)
        {
            string val = record.CustomFields[GeminiWarcCreator.WarcProtocolField].First();
            record.CustomFields.RemoveAll(GeminiWarcCreator.WarcProtocolField);
            record.CustomFields.Add(GeminiWarcCreator.WarcProtocolField, "gemini");
            record.CustomFields.Add(GeminiWarcCreator.WarcProtocolField, val);
        }
    }

    private static void FixTlsCipherSuite(WarcRecord record)
    {
        const string tlsCipherField = "WARC-TLS-Cipher-Suite";

        //using the old version?
        if (record.CustomFields.ContainsField(tlsCipherField))
        {
            //update it
            string val = record.CustomFields[tlsCipherField].First();
            record.CustomFields.RemoveAll(tlsCipherField);
            record.CustomFields.Add(GeminiWarcCreator.WarcCipherSuiteField, val);
        }
        else if (record.CustomFields.ContainsField(GeminiWarcCreator.WarcCipherSuiteField))
        {
            //fix the weird lower-casing that happened by removing and re-adding
            string val = record.CustomFields[GeminiWarcCreator.WarcCipherSuiteField].First();
            record.CustomFields.RemoveAll(GeminiWarcCreator.WarcCipherSuiteField);
            record.CustomFields.Add(GeminiWarcCreator.WarcCipherSuiteField, val);
        }
    }

    public static void Fix(string inputWarc, string outputWarc)
    {
        DateTime prev = DateTime.Now;
        int Processed = 0;
        using (WarcWriter writer = new WarcWriter(outputWarc))
        {
            using (WarcReader reader = new WarcReader(inputWarc))
            {
                foreach (var record in reader)
                {
                    Processed++;
                    if (record is RequestRecord requestRecord)
                    {
                        //Clear any payload digest from a request
                        requestRecord.PayloadDigest = null;
                        FixRobotsRequestRecord(requestRecord);
                    }

                    if (record is ResponseRecord responseRecord)
                    {
                        FixRobotsResponseRecord(responseRecord);
                    }

                    //remove any block digest
                    if (record is WarcInfoRecord infoRecord)
                    {
                        infoRecord.BlockDigest = null;
                    }

                    writer.Write(record);
                }
            }
        }

        int seconds = (int)DateTime.Now.Subtract(prev).TotalSeconds;
        if (seconds == 0)
        {
            seconds = 1;
        }

        Console.WriteLine($"Records: {Processed}\tTime: {seconds}s\tRate: {Processed / seconds} / s");
    }

    private static void UpdateBlockDigest(WarcRecord record)
    {
        if (record.ContentLength > 0)
        {
            record.BlockDigest = GeminiParser.GetStrongHash(record.ContentBlock!);
        }
    }

    private static void FixRobotsRequestRecord(RequestRecord requestRecord)
    {
        if (requestRecord.TargetUri == null)
        {
            return;
        }

        if (!requestRecord.TargetUri.AbsoluteUri.Contains("/robots.txt?kennedy-crawler"))
        {
            return;
        }

        string cleanUrl = requestRecord.TargetUri.AbsoluteUri.Replace("/robots.txt?kennedy-crawler", "/robots.txt");

        GeminiUrl geminiUrl = new GeminiUrl(cleanUrl);

        requestRecord.TargetUri = geminiUrl.Url;
        requestRecord.ContentBlock = GeminiParser.CreateRequestBytes(geminiUrl);
        UpdateBlockDigest(requestRecord);
    }

    private static void FixRobotsResponseRecord(ResponseRecord responseRecord)
    {
        if (responseRecord.TargetUri == null)
        {
            return;
        }

        if (!responseRecord.TargetUri.AbsoluteUri.Contains("/robots.txt?kennedy-crawler"))
        {
            return;
        }

        string cleanUrl = responseRecord.TargetUri.AbsoluteUri.Replace("/robots.txt?kennedy-crawler", "/robots.txt");

        GeminiUrl geminiUrl = new GeminiUrl(cleanUrl);
        responseRecord.TargetUri = geminiUrl.Url;
    }

    private static void FixResponsePayload(ResponseRecord responseRecord)
    {

        if (responseRecord.IdentifiedPayloadType != null && responseRecord.PayloadDigest != null)
        {
            //nothing to do here
            return;
        }

        if (responseRecord.TargetUri == null)
        {
            throw new ArgumentException("Target URI is null!");
        }

        if (responseRecord.ContentBlock == null)
        {
            throw new ArgumentException("Content Block is empty?!?!");
        }

        var geminiUrl = new GeminiUrl(responseRecord.TargetUri);
        var response = GeminiParser.ParseResponseBytes(geminiUrl, responseRecord.ContentBlock);

        //ensure a identified payload type exists
        if (responseRecord.IdentifiedPayloadType == null)
        {
            responseRecord.IdentifiedPayloadType = response.MimeType;
        }

        if (responseRecord.PayloadDigest == null && response.HasBody)
        {
            responseRecord.PayloadDigest = GeminiParser.GetStrongHash(response.BodyBytes!);
        }
    }

    //private static void OptimizeForStoage(ResponseRecord response)
    //{
    //    if(response.IdentifiedPayloadType == null)
    //    {
    //        return;
    //    }

    //    if (response.IdentifiedPayloadType.StartsWith("text/") || response.IdentifiedPayloadType.StartsWith("image/"))
    //    {
    //        return;
    //    }

    //    if (response.ContentLength > MaxUninterestingFileSize)
    //    {
    //        response.ContentBlock = response.ContentBlock!.Take(MaxUninterestingFileSize).ToArray();
    //        response.Truncated = "length";
    //          //TODO: Update the response digests!
    //    }
    //}
}