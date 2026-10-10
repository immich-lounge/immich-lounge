using System.Collections.Generic;

namespace ImmichLoungeCompanion.Immich.Dtos;

public class ImmichMemory
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public ImmichMemoryData? Data { get; set; }
    public List<ImmichAsset> Assets { get; set; } = [];
}

public class ImmichMemoryData
{
    public int Year { get; set; }
    public string? PersonId { get; set; }
    public string? PersonName { get; set; }
}
