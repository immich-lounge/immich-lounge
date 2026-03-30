using System.Collections.Generic;

namespace ImmichLoungeCompanion.Immich.Dtos;

public class SearchAssetsRequest
{
    public List<string>? AlbumIds { get; set; }
    public List<string>? PersonIds { get; set; }
    public List<string>? TagIds { get; set; }
    public string? TakenAfter { get; set; }         // ISO 8601 date
    public string? TakenBefore { get; set; }
    public string? Type { get; set; }               // "IMAGE" | "VIDEO" (optional filter)
    public bool WithExif { get; set; } = true;
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 1000;
}
