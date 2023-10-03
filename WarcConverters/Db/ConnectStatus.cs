using System;
namespace Kennedy.WarcConverters.Db
{
    /// <summary>
    /// Status of the network connection made for a Gemini request.
    /// Used to show errors at the network level vs protocol level
    /// </summary>
    public enum ConnectStatus : int
    {
        Unknown = 0,
        Success = 1,
        Error = 2,
        Skipped = 3,
    }
}

