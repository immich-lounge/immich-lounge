using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Immich;

public interface IImmichClient
{
    /// <summary>Tests connectivity; returns image/video counts on success.</summary>
    Task<(bool Ok, int ImageCount, int VideoCount, string? Error)> TestConnectionAsync(ImmichSettings settings);
    Task<SearchApiCapabilities> GetSearchApiCapabilitiesAsync(ImmichSettings settings, bool refresh = false);
    Task<List<ImmichAlbum>> GetAlbumsAsync(ImmichSettings settings);
    Task<List<ImmichPerson>> GetPeopleAsync(ImmichSettings settings);
    Task<List<string>> GetLocationSuggestionsAsync(ImmichSettings settings, string type, string? country = null, string? state = null);
    Task<List<ImmichTag>> GetTagsAsync(ImmichSettings settings);
    /// <summary>Fetches ALL pages for a search using the server-compatible format. Preserves date and location filters.</summary>
    Task<List<ImmichAsset>> SearchAssetsAllPagesAsync(ImmichSettings settings, SearchAssetsRequest request);
    Task<List<ImmichMemory>> GetMemoriesAsync(ImmichSettings settings, DateOnly date);
    Task<ImmichAsset?> GetAssetAsync(ImmichSettings settings, string assetId, CancellationToken ct = default);
}
