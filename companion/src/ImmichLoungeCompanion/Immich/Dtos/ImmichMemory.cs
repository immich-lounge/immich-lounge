using System.Collections.Generic;

namespace ImmichLoungeCompanion.Immich.Dtos;

public class ImmichMemory
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public List<ImmichAsset> Assets { get; set; } = [];
}
