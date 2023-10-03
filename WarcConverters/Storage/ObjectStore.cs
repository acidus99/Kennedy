namespace Kennedy.WarcConverters.Storage;

/// <summary>
/// disk-backed KV store, using a directory structure
/// </summary>
internal class ObjectStore
{
    string RootDir;
    char[] InvalidPathChars;
    char[] InvalidFileChars;

    public ObjectStore(string path)
    {
        RootDir = path;
        if (!RootDir.EndsWith(Path.DirectorySeparatorChar))
        {
            RootDir += Path.DirectorySeparatorChar;
        }
        InvalidPathChars = Path.GetInvalidPathChars();
        InvalidFileChars = Path.GetInvalidFileNameChars();
    }

    private string getPrefixDirectoryForKey(string key)
    {
        if(key.Length < 4)
        {
            return RootDir;
        }
        return RootDir + key[0] + key[1] + Path.DirectorySeparatorChar + key[2] + key[3] + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// checks that a key doesn't contain illegal characters (e.g. illegal characters for path or filename)
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private bool IsKeyIsValid(string key)
    {
        for(int i=0, len = key.Length; i < len; i++)
        {
            char c = key[i];
            if (i < 4 && InvalidPathChars.Contains(c))
            {
                return false;
            }
            if(InvalidFileChars.Contains(c))
            {
                return false;
            }
        }
        return true;
    }

    public byte [] GetObject(string key)
    {
        if (!IsKeyIsValid(key))
        {
            throw new ArgumentException("Key contains invalid characters", "key");
        }

        var dir = getPrefixDirectoryForKey(key);
        return File.ReadAllBytes(dir + key);
    }
}
