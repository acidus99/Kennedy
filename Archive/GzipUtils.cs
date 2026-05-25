using System.IO.Compression;

namespace Kennedy.Archive;

/// <summary>
/// Thin wrappers around <see cref="System.IO.Compression.GZipStream"/> for in-memory compression.
/// Used by the pack file system to optionally compress response bytes before writing to disk.
/// </summary>
public static class GzipUtils
{
    /// <summary>Compresses <paramref name="data"/> using GZip with maximum compression and returns the compressed bytes.</summary>
    public static byte[] Compress(byte[] data)
    {
        using (var compressedStream = new MemoryStream())
        using (var zipStream = new GZipStream(compressedStream, CompressionLevel.SmallestSize))
        {
            zipStream.Write(data, 0, data.Length);
            zipStream.Close();
            return compressedStream.ToArray();
        }
    }

    /// <summary>Decompresses GZip-compressed <paramref name="data"/> and returns the original bytes.</summary>
    public static byte[] Decompress(byte[] data)
    {
        using (var compressedStream = new MemoryStream(data))
        using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            zipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
}