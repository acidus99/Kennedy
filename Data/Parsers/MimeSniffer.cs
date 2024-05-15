namespace Kennedy.Data.Parsers;

/// <summary>
/// Implments various algorithms from the Mime Sniffing Living Standard
/// https://mimesniff.spec.whatwg.org/#pattern-mask
/// </summary>
public class MimeSniffer
{
    const int ResourceHeaderLength = 1445;

    /// <summary>
    /// Implements the "mislabeled binary resource" rules in section 7.2 
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public bool IsText(byte[] data)
    {
        var header = GetResourceHeader(data);

        if (header.Length >= 2 &&
            ((header[0] == 0xFE && header[1] == 0xFF) ||
                (header[0] == 0xFF && header[1] == 0xFE))
           )
        {
            //TODO: this indicates UTF-16 encoding. Perhaps change the string-from-bytes logic?
            return true;
        }
        if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            return true;
        }
        foreach (var b in header)
        {
            if (IsBinaryByte(b))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Implements reading the resource header, as defined in section 5.2
    /// </summary>
    /// <returns></returns>
    private byte[] GetResourceHeader(byte[] data)
    {
        if (data.Length > ResourceHeaderLength)
        {
            return data.Take(ResourceHeaderLength).ToArray();
        }
        return data;
    }

    /// <summary>
    /// Implements the binary data byte check from section 3
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    private bool IsBinaryByte(byte b)
    {
        if (b >= 0x00 && b <= 0x08)
        {
            return true;
        }
        if (b == 0x0B)
        {
            return true;
        }
        if (b >= 0x0E && b <= 0x1A)
        {
            return true;
        }
        if (b >= 0x1C && b <= 0x1F)
        {
            return true;
        }
        return false;
    }
}