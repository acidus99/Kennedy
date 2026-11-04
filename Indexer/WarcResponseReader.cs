using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using WarcDotNet;

namespace Kennedy.Indexer;

/// <summary>
/// Response-only import reader. Buffers decompressed headers and drains unrelated
/// record bodies without allocating them. Certificate export still uses WarcReader.
/// </summary>
internal sealed class WarcResponseReader : IDisposable
{
    private readonly FileStream _file;
    private readonly Stream _input;
    private readonly byte[] _line = new byte[64*1024];
    private readonly byte[] _trailer = new byte[4];
    public int RecordsRead { get; private set; }
    public long CompressedBytesRead => _file.Position;
    public long FileLength => _file.Length;

    public WarcResponseReader(string path)
    {
        _file = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.SequentialScan);
        Stream content = path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(_file,CompressionMode.Decompress) : _file;
        _input = new BufferedStream(content,128*1024);
    }

    public IEnumerable<ResponseRecord> ReadResponses(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            string? version = ReadLine();
            if (version == null) yield break;
            RecordsRead++;
            if (!version.StartsWith("WARC/",StringComparison.OrdinalIgnoreCase)) throw Malformed("Missing WARC version.");
            string? type=null,target=null,date=null,truncated=null;
            int? length=null;
            Uri? id=null;
            string? line;
            while ((line=ReadLine()) != null)
            {
                int colon=line.IndexOf(':');
                if (colon<=0 || colon==line.Length-1) throw Malformed("Malformed WARC field.");
                string name=line[..colon],value=line[(colon+1)..].Trim();
                if (name.Equals("WARC-Type",StringComparison.OrdinalIgnoreCase)) type=value;
                else if (name.Equals("Content-Length",StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int size)) throw Malformed("Invalid Content-Length.");
                    length=size;
                }
                else if (name.Equals(WarcFields.TargetUri,StringComparison.OrdinalIgnoreCase)) target=value;
                else if (name.Equals(WarcFields.Date,StringComparison.OrdinalIgnoreCase)) date=value;
                else if (name.Equals(WarcFields.Truncated,StringComparison.OrdinalIgnoreCase)) truncated=value;
                else if (name.Equals(WarcFields.RecordId,StringComparison.OrdinalIgnoreCase)) id=new Uri(value.Trim('<','>'));
            }
            if (type==null || length==null) throw Malformed("Missing WARC type or Content-Length.");
            bool response=type.Equals("response",StringComparison.OrdinalIgnoreCase);
            byte[]? body=response ? new byte[length.Value] : null;
            try
            {
                if (body!=null) _input.ReadExactly(body);
                else Drain(length.Value,ct);
                _input.ReadExactly(_trailer);
            }
            catch (EndOfStreamException) { throw Malformed("Incomplete record body or trailer."); }
            if (!_trailer.AsSpan().SequenceEqual("\r\n\r\n"u8)) throw Malformed("Missing record CRLFCRLF trailer.");
            if (response)
            {
                var record=new ResponseRecord
                {
                    Version=version[5..], ContentBlock=body,
                    TargetUri=target==null ? null : new Uri(target.Trim('<','>')),
                    Truncated=truncated
                };
                if (date!=null) record.Date=DateTime.Parse(date,CultureInfo.InvariantCulture);
                if (id!=null) record.Id=id;
                yield return record;
            }
        }
    }

    private string? ReadLine()
    {
        int length=0;
        while (length<_line.Length)
        {
            int value=_input.ReadByte();
            if (value<0)
            {
                if (length==0) return null;
                throw Malformed("Incomplete WARC header.");
            }
            if (value==13)
            {
                if (_input.ReadByte()!=10) throw Malformed("CR without LF in WARC header.");
                return length==0 ? null : Encoding.UTF8.GetString(_line,0,length);
            }
            if (value<32 || value==127) throw Malformed("Control character in WARC header.");
            _line[length++]=(byte)value;
        }
        throw Malformed("WARC header line exceeds 64KB.");
    }

    private void Drain(int length,CancellationToken ct)
    {
        byte[] buffer=ArrayPool<byte>.Shared.Rent(64*1024);
        try
        {
            while (length>0)
            {
                ct.ThrowIfCancellationRequested();
                int count=Math.Min(length,buffer.Length);
                _input.ReadExactly(buffer.AsSpan(0,count)); length-=count;
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private WarcFormatException Malformed(string message) => new(message,RecordsRead,null);
    public void Dispose() => _input.Dispose();
}
