namespace Kennedy.Archive.Pack;

/// <summary>
/// Stores a group of packs on disk and an efficient way
/// </summary>
public class PackManager
{
    string ArchiveRoot;
    char[] InvalidPathChars;
    char[] InvalidFileChars;

    public PackManager(string path)
    {
        ArchiveRoot = path;
        if (!ArchiveRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            ArchiveRoot += Path.DirectorySeparatorChar;
        }
        InvalidPathChars = Path.GetInvalidPathChars();
        InvalidFileChars = Path.GetInvalidFileNameChars();
    }

    /// <summary>
    /// checks that a key doesn't contain illegal characters (e.g. illegal characters for path or filename)
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private bool IsKeyIsValid(string key)
    {
        for (int i = 0, len = key.Length; i < len; i++)
        {
            char c = key[i];
            if (i < 4 && InvalidPathChars.Contains(c))
            {
                return false;
            }
            if (InvalidFileChars.Contains(c))
            {
                return false;
            }
        }
        return true;
    }

    private string GetPathForPackName(string packName)
    {
        return ArchiveRoot + Path.DirectorySeparatorChar + packName[0] + packName[1] + Path.DirectorySeparatorChar +
            packName[2] + packName[3] + Path.DirectorySeparatorChar;
    }

    public PackFile GetPack(string dataHash)
    {
        var index = dataHash.IndexOf(':');

        if (index < 0 || index == dataHash.Length)
        {
            throw new ArgumentException("Hash is not of the form '[hash name]:[hexadecimal string]'");
        }

        dataHash = dataHash.Substring(index + 1);

        if (dataHash.Length < 4)
        {
            throw new ArgumentException($"PackID is too short! Expected > 4, got {dataHash.Length}");
        }

        if (!IsKeyIsValid(dataHash))
        {
            throw new ArgumentException("Packname contains invalid characters", "packName");
        }

        var path = GetPathForPackName(dataHash);
        return new PackFile(path, GetPackFileName(dataHash));
    }

    private string GetPackFileName(string packName)
    {
        if (packName.Length < 4)
        {
            throw new ArgumentException($"PackID is too short! Expected > 4, got {packName.Length}");
        }

        if (!IsKeyIsValid(packName))
        {
            throw new ArgumentException("Packname contains invalid characters", "packName");
        }
        return $"{packName[0]}{packName[1]}{packName[2]}{packName[3]}";
    }


    public bool DeletePack(string packName)
    {
        if (!IsKeyIsValid(packName))
        {
            throw new ArgumentException("Packname contains invalid characters", "packName");
        }

        var path = GetPathForPackName(packName);
        if (File.Exists(path + packName))
        {
            File.Delete(path + packName);
            return true;
        }
        return false;
    }
}
