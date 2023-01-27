namespace Kennedy.Archive.Pack
{
    /// <summary>
    /// Stores a group of packs on disk and an efficient way
    /// </summary>
    public class PackManager
    {
        string ArchiveRoot;

        public PackManager(string path)
        {
            ArchiveRoot = path;
            if(!ArchiveRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                ArchiveRoot += Path.DirectorySeparatorChar;
            }
        }

        public PackFile GetPack(string packName)
        {
            if(packName.Length < 4)
            {
                throw new ArgumentException($"PackID is too short! Expected > 4, got {packName.Length}");
            }

            var path = ArchiveRoot + Path.DirectorySeparatorChar + packName[0] + Path.DirectorySeparatorChar +
                packName[1] + Path.DirectorySeparatorChar;

            //Ensure the file path exists
            Directory.CreateDirectory(path);

            return new PackFile(path + packName);
        }
    }
}
