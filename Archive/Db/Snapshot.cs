﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemini.Net;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive.Db;

[Table("Snapshots")]
[Index(nameof(DataHash))]
[Index(nameof(UrlId))]
[Index(nameof(Captured))]
public class Snapshot
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The offset of this snapshot in the Pack file
    /// </summary>
    public long Offset { get; set; }

    //The uncompressed size of the response for this snapshot
    public long Size { get; set; }

    /// <summary>
    /// Is this snapshit a duplicate of a previous snapshot?
    /// </summary>
    public bool IsDuplicate { get; set; } = false;

    public bool IsGlobalDuplicate { get; set; } = false;

    public int StatusCode { get; set; }

    //Sha-256 hex string
    public required string DataHash { get; set; }

    public string? Mimetype { get; set; }

    public DateTime Captured { get; set; }

    public long UrlId { get; set; }

    public Url? Url { get; set; }

    public bool HasBodyContent { get; set; }

    public bool IsBodyTruncated { get; set; }

    public bool IsSuccess
        => GeminiParser.IsSuccessStatus(StatusCode);

    public bool IsGemtext
        => Mimetype?.StartsWith("text/gemini") ?? false;

    public bool IsText
        => Mimetype?.StartsWith("text/") ?? false;
}

