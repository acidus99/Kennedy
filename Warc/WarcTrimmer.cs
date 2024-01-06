using WarcDotNet;

namespace Kennedy.Warc;

public static class WarcTrimmer
{
    public static void CreateTrimmedWarc(string inputWarc, string outputWarc, int recordCount)
    {
        if (!File.Exists(inputWarc))
        {
            throw new ArgumentException("File does not exist", nameof(inputWarc));
        }

        int writtenCount = 0;
        using (WarcWriter writer = new WarcWriter(outputWarc))
        {
            using (WarcReader reader = new WarcReader(inputWarc))
            {
                foreach (var record in reader)
                {
                    if (writtenCount < recordCount)
                    {
                        writer.Write(record);
                        writtenCount++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        Console.WriteLine($"Wrote {writtenCount} records to '{outputWarc}'");
    }
}
