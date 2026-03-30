using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Immich;

public interface IImmichClient
{
    /// <summary>Tests connectivity; returns image/video counts on success.</summary>
    Task<(bool Ok, int ImageCount, int VideoCount, string? Error)> TestConnectionAsync(ImmichSettings settings);
    Task<List<ImmichAlbum>> GetAlbumsAsync(ImmichSettings settings);
    Task<List<ImmichPerson>> GetPeopleAsync(ImmichSettings settings);
    Task<List<ImmichTag>> GetTagsAsync(ImmichSettings settings);
    /// <summary>Fetches ALL pages for a single source. Applies date filter if provided.</summary>
    Task<List<ImmichAsset>> SearchAssetsAllPagesAsync(ImmichSettings settings, SearchAssetsRequest request);
    Task<List<ImmichMemory>> GetMemoriesAsync(ImmichSettings settings, DateOnly date);
}
