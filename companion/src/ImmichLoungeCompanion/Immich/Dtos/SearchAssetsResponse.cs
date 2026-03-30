using System.Collections.Generic;

namespace ImmichLoungeCompanion.Immich.Dtos;

public class SearchAssetsResponse
{
    public SearchAssetsPage Assets { get; set; } = new();
}

public class SearchAssetsPage
{
    public List<ImmichAsset> Items { get; set; } = [];
    public string? NextPage { get; set; }
    public int Total { get; set; }
}
