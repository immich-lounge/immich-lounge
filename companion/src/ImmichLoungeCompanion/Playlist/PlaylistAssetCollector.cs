using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistAssetCollector(IImmichClient immich)
{
    public async Task<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> CollectAsync(
        Profile profile,
        ImmichSettings immichSettings,
        CancellationToken ct = default)
    {
        profile.NormalizeAssetFilter();

        Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> allAssets = profile.AssetFilter == null
            ? await FetchAllAssetsAsync(immichSettings, profile.DateFilter, ct)
            : await EvaluateRuleAsync(profile.AssetFilter, immichSettings, profile.DateFilter, ct);

        if (profile.HasMemoriesSource())
        {
            var memoryAssets = await FetchMemoriesAssetsAsync(immichSettings, ct);
            foreach (var (id, asset) in memoryAssets)
            {
                allAssets.TryAdd(id, (asset, null));
            }
        }

        ApplyQualityFilter(allAssets, profile.Quality);
        return allAssets;
    }

    private async Task<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> FetchAllAssetsAsync(
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var request = new SearchAssetsRequest();
        if (dateFilter != null)
        {
            ApplyDateFilter(request, dateFilter);
        }

        var assets = await immich.SearchAssetsAllPagesAsync(immichSettings, request);
        return ToAssetDictionary(assets, sourceLabel: null);
    }

    private async Task<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> EvaluateRuleAsync(
        AssetFilterRule rule,
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (rule.Kind == AssetFilterRuleKind.Condition)
        {
            return await FetchConditionAssetsAsync(rule, immichSettings, dateFilter, ct);
        }

        var childResults = new List<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>>();
        foreach (var child in rule.Children)
        {
            childResults.Add(await EvaluateRuleAsync(child, immichSettings, dateFilter, ct));
        }

        if (childResults.Count == 0)
        {
            return new Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>(StringComparer.Ordinal);
        }

        return rule.Operator switch
        {
            AssetFilterGroupOperator.And => Intersect(childResults),
            _ => Union(childResults)
        };
    }

    private async Task<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> FetchConditionAssetsAsync(
        AssetFilterRule rule,
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var request = BuildSearchRequest(rule, dateFilter);
        var assets = await immich.SearchAssetsAllPagesAsync(immichSettings, request);
        var sourceLabel = rule.Type == AssetFilterConditionType.Album ? rule.Label : null;

        return ToAssetDictionary(assets, sourceLabel);
    }

    private async Task<Dictionary<string, ImmichAsset>> FetchMemoriesAssetsAsync(
        ImmichSettings immichSettings,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var memories = await immich.GetMemoriesAsync(immichSettings, today);
        var results = new Dictionary<string, ImmichAsset>(StringComparer.Ordinal);

        foreach (var memory in memories)
        {
            foreach (var asset in memory.Assets)
            {
                results.TryAdd(asset.Id, asset);
            }
        }

        return results;
    }

    private static Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> Union(
        IEnumerable<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> childResults)
    {
        var combined = new Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>(StringComparer.Ordinal);

        foreach (var child in childResults)
        {
            foreach (var (id, asset) in child)
            {
                combined.TryAdd(id, asset);
            }
        }

        return combined;
    }

    private static Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> Intersect(
        IReadOnlyList<Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>> childResults)
    {
        var intersection = new Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>(childResults[0], StringComparer.Ordinal);

        foreach (var key in intersection.Keys.ToList())
        {
            var matchingEntries = new List<(ImmichAsset Asset, string? SourceLabel)> { intersection[key] };
            var missingFromChild = false;

            for (var i = 1; i < childResults.Count; i++)
            {
                if (!childResults[i].TryGetValue(key, out var value))
                {
                    missingFromChild = true;
                    break;
                }

                matchingEntries.Add(value);
            }

            if (missingFromChild)
            {
                intersection.Remove(key);
                continue;
            }

            intersection[key] = matchingEntries.Count == 1
                ? matchingEntries[0]
                : (matchingEntries[0].Asset, null);
        }

        return intersection;
    }

    private static Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> ToAssetDictionary(
        IEnumerable<ImmichAsset> assets,
        string? sourceLabel)
    {
        var results = new Dictionary<string, (ImmichAsset Asset, string? SourceLabel)>(StringComparer.Ordinal);

        foreach (var asset in assets)
        {
            results.TryAdd(asset.Id, (asset, sourceLabel));
        }

        return results;
    }

    private static void ApplyQualityFilter(
        Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> assets,
        QualitySettings quality)
    {
        if (quality.MinFileSizeKb is not > 0)
        {
            return;
        }

        var minFileSizeBytes = quality.MinFileSizeKb.Value * 1024L;
        foreach (var key in assets.Keys.ToList())
        {
            var asset = assets[key].Asset;
            var fileSize = asset.ExifInfo?.FileSizeInByte;
            if (fileSize is null || fileSize.Value < minFileSizeBytes)
            {
                assets.Remove(key);
            }
        }
    }

    private static SearchAssetsRequest BuildSearchRequest(AssetFilterRule rule, DateFilter? dateFilter)
    {
        var request = new SearchAssetsRequest();

        switch (rule.Type)
        {
            case AssetFilterConditionType.Album:  request.AlbumIds = [rule.Id!]; break;
            case AssetFilterConditionType.Person: request.PersonIds = [rule.Id!]; break;
            case AssetFilterConditionType.Tag:    request.TagIds = [rule.Id!]; break;
        }

        if (dateFilter != null)
        {
            ApplyDateFilter(request, dateFilter);
        }

        return request;
    }

    private static void ApplyDateFilter(SearchAssetsRequest request, DateFilter filter)
    {
        if (filter.Type == "range")
        {
            request.TakenAfter = filter.From;
            request.TakenBefore = filter.To;
        }
        else if (filter.Type == "rolling" && filter.Amount.HasValue)
        {
            var now = DateTime.UtcNow;
            var from = filter.Unit switch
            {
                "days"   => now.AddDays(-filter.Amount.Value),
                "weeks"  => now.AddDays(-filter.Amount.Value * 7),
                "months" => now.AddMonths(-filter.Amount.Value),
                "years"  => now.AddYears(-filter.Amount.Value),
                _        => now
            };
            request.TakenAfter = from.ToString("yyyy-MM-dd");
        }
    }
}
