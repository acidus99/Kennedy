namespace Kennedy.Archive.Pack
{
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
            if(!ArchiveRoot.EndsWith(Path.DirectorySeparatorChar))
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

        private string getPathForPackName(string packName)
        {
            var path = ArchiveRoot + Path.DirectorySeparatorChar + packName[0] + packName[1] + Path.DirectorySeparatorChar +
                packName[2] + packName[3] + Path.DirectorySeparatorChar;
        }

        public PackFile GetPack(string packName)
        {
            if(packName.Length < 4)
            {
                throw new ArgumentException($"PackID is too short! Expected > 4, got {packName.Length}");
            }

            if (!IsKeyIsValid(packName))
            {
                throw new ArgumentException("Packname contains invalid characters", "packName");
            }

            var path = ArchiveRoot + Path.DirectorySeparatorChar + packName[0] + packName[1] + Path.DirectorySeparatorChar +
                packName[2] + packName[3]+ Path.DirectorySeparatorChar;

            //Ensure the file path exists
            Directory.CreateDirectory(path);

            return new PackFile(path + packName);
        }

        public void DeletePack(string packName)
        {
            if (!IsKeyIsValid(packName))
            {
                throw new ArgumentException("Packname contains invalid characters", "packName");
            }
        }
    }
}
