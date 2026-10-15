using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistAssetCollector(IImmichClient immich, TimeProvider? timeProvider = null,
    ILogger<PlaylistAssetCollector>? logger = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ILogger<PlaylistAssetCollector> _logger = logger ?? NullLogger<PlaylistAssetCollector>.Instance;
    public async Task<Dictionary<string, CollectedAsset>> CollectAsync(
        Profile profile,
        ImmichSettings immichSettings,
        CancellationToken ct = default)
    {
        profile.NormalizeAssetFilter();
        if (profile.OnlyPhotosWithPeople &&
            (await immich.GetSearchApiCapabilitiesAsync(immichSettings))?.StructuredSearch != true)
        {
            throw new NotSupportedException("Only photos with people requires Immich 3.2 or later. Disable this filter or upgrade Immich.");
        }

        var source = profile.EffectivePhotoSource();
        var albumIds = source != "selected" || profile.AssetFilter == null
            ? new List<string>()
            : FindAlbumIds(profile.AssetFilter).Distinct(StringComparer.Ordinal).ToList();

        Dictionary<string, CollectedAsset> allAssets = source == "selected" && profile.AssetFilter != null
            ? await EvaluateRuleAsync(profile.AssetFilter, immichSettings, profile.DateFilter, profile.LocationFilter, albumIds, profile.OnlyPhotosWithPeople, ct)
            : source == "all"
                ? await FetchAllAssetsAsync(immichSettings, profile.DateFilter, profile.LocationFilter, profile.OnlyPhotosWithPeople, ct)
                : new(StringComparer.Ordinal);

        if (profile.HasMemoriesSource())
        {
            // Explicit sources constrain memories too. Legacy profiles retain their additive behavior.
            var dateMatches = profile.PhotoSource != null && source == "memories" && profile.DateFilter != null
                ? await FetchAllAssetsAsync(immichSettings, profile.DateFilter, profile.LocationFilter, profile.OnlyPhotosWithPeople, ct)
                : null;
            var memoryAssets = await FetchOptionalMemoriesAsync(profile, immichSettings, allAssets.Count > 0, ct);
            if (profile.OnlyPhotosWithPeople && memoryAssets.Count > 0)
            {
                // Memories can omit faces and lie outside the regular date range.
                // Use the server's face predicate, including selected shared album scopes.
                var matching = await SearchPeopleAssetsAsync(immichSettings, null, albumIds,
                    null, profile.LocationFilter, ct, onlyPhotosWithPeople: true);
                var matchingAssets = matching.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
                foreach (var id in memoryAssets.Keys.ToList())
                {
                    if (matchingAssets.TryGetValue(id, out var matched))
                    {
                        memoryAssets[id] = memoryAssets[id] with { Asset = matched };
                    }
                    else { memoryAssets.Remove(id); }
                }
            }
            foreach (var (id, asset) in memoryAssets)
            {
                ct.ThrowIfCancellationRequested();
                allAssets.TryGetValue(id, out var existing);
                if (profile.PhotoSource != null && source != "memories" && existing == null)
                {
                    continue;
                }
                if (dateMatches != null && !dateMatches.ContainsKey(id))
                {
                    continue;
                }
                var candidate = existing?.Asset ?? asset.Asset;
                // Memory responses may omit EXIF. Reuse search metadata or hydrate only
                // when a filter needs it; never interpret an abbreviated response as no match.
                if (NeedsMemoryMetadata(profile) && candidate.ExifInfo == null)
                {
                    candidate = await immich.GetAssetAsync(immichSettings, id, ct);
                }
                if (candidate != null && MatchesLocation(candidate, profile.LocationFilter))
                {
                    if (existing != null)
                    {
                        // Keep album context while attaching the birthday context once.
                        allAssets[id] = existing with { Asset = candidate, BirthdayCaption = asset.BirthdayCaption ?? existing.BirthdayCaption, IsMemory = true };
                    }
                    else
                    {
                        allAssets.Add(id, asset with { Asset = candidate, IsMemory = true });
                    }
                }
            }
        }

        // Query each person separately: ID-list matching differs across Immich API versions.
        // Exclusions cover memories too, including those outside the profile's date range.
        foreach (var personId in profile.ExcludedPeople.Select(person => person.Id).Distinct(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var excluded = await SearchPeopleAssetsAsync(immichSettings, personId, albumIds, null, null, ct);
            foreach (var asset in excluded)
            {
                allAssets.Remove(asset.Id);
            }
        }

        await ApplyCurationFiltersAsync(allAssets, profile, immichSettings, ct);
        ApplyQualityFilter(allAssets, profile.Quality);
        return allAssets;
    }

    private async Task ApplyCurationFiltersAsync(Dictionary<string, CollectedAsset> assets,
        Profile profile, ImmichSettings settings, CancellationToken ct)
    {
        if (!profile.OnlyFavorites && profile.MinimumRating == null) { return; }

        // Filter the combined selection so legacy search, shared albums and memories
        // use the same minimum-rating semantics. Legacy search only supports exact ratings.
        foreach (var id in assets.Keys.ToList())
        {
            ct.ThrowIfCancellationRequested();
            var entry = assets[id];
            var asset = entry.Asset;
            // Sanitized/abbreviated responses can omit favorite or EXIF metadata.
            if ((profile.OnlyFavorites && asset.IsFavorite == null) ||
                (profile.MinimumRating != null && asset.ExifInfo == null))
            {
                asset = await immich.GetAssetAsync(settings, id, ct);
            }

            if (asset == null || (profile.OnlyFavorites && asset.IsFavorite != true) ||
                (profile.MinimumRating is { } minimum && !(asset.ExifInfo?.Rating >= minimum)))
            {
                assets.Remove(id);
            }
            else
            {
                assets[id] = entry with { Asset = asset };
            }
        }
    }

    private async Task<Dictionary<string, CollectedAsset>> FetchOptionalMemoriesAsync(
        Profile profile, ImmichSettings settings, bool hasRegularAssets, CancellationToken ct)
    {
        try
        {
            return await FetchMemoriesAssetsAsync(profile, settings, ct);
        }
        catch (Exception ex) when (hasRegularAssets && !ct.IsCancellationRequested && IsTransientMemoryFailure(ex))
        {
            _logger.LogWarning(ex, "Memories unavailable for profile {ProfileId}; continuing with regular photos until the next refresh", profile.Id);
            return new(StringComparer.Ordinal);
        }
    }

    private static bool IsTransientMemoryFailure(Exception ex) => ex is OperationCanceledException ||
        ex is HttpRequestException request && (request.StatusCode == null ||
            request.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)request.StatusCode.Value >= 500);

    private static bool NeedsMemoryMetadata(Profile profile) =>
        profile.Quality.MinFileSizeKb is > 0 || CleanLocation(profile.LocationFilter?.Country) != null ||
        CleanLocation(profile.LocationFilter?.State) != null || CleanLocation(profile.LocationFilter?.City) != null;

    private async Task<Dictionary<string, CollectedAsset>> FetchAllAssetsAsync(
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        LocationFilter? locationFilter,
        bool onlyPhotosWithPeople,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var request = new SearchAssetsRequest { HasPeople = onlyPhotosWithPeople, Type = onlyPhotosWithPeople ? "IMAGE" : null };
        if (dateFilter != null)
        {
            ApplyDateFilter(request, dateFilter);
        }

        ApplyLocationFilter(request, locationFilter);
        var assets = await immich.SearchAssetsAllPagesAsync(immichSettings, request);
        return ToAssetDictionary(assets, sourceLabel: null);
    }

    private async Task<Dictionary<string, CollectedAsset>> EvaluateRuleAsync(
        AssetFilterRule rule,
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        LocationFilter? locationFilter,
        IReadOnlyList<string> albumIds,
        bool onlyPhotosWithPeople,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (rule.Kind == AssetFilterRuleKind.Condition)
        {
            return await FetchConditionAssetsAsync(rule, immichSettings, dateFilter, locationFilter, albumIds, onlyPhotosWithPeople, ct);
        }

        var childResults = new List<Dictionary<string, CollectedAsset>>();
        foreach (var child in rule.Children)
        {
            childResults.Add(await EvaluateRuleAsync(child, immichSettings, dateFilter, locationFilter, albumIds, onlyPhotosWithPeople, ct));
        }

        if (childResults.Count == 0)
        {
            return new Dictionary<string, CollectedAsset>(StringComparer.Ordinal);
        }

        return rule.Operator switch
        {
            AssetFilterGroupOperator.And => Intersect(childResults),
            _ => Union(childResults)
        };
    }

    private async Task<Dictionary<string, CollectedAsset>> FetchConditionAssetsAsync(
        AssetFilterRule rule,
        ImmichSettings immichSettings,
        DateFilter? dateFilter,
        LocationFilter? locationFilter,
        IReadOnlyList<string> albumIds,
        bool onlyPhotosWithPeople,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (rule.Type == AssetFilterConditionType.Person)
        {
            var peopleAssets = await SearchPeopleAssetsAsync(immichSettings, rule.Id!, albumIds, dateFilter, locationFilter, ct, onlyPhotosWithPeople);
            return ToAssetDictionary(peopleAssets, null);
        }

        var request = BuildSearchRequest(rule, dateFilter);
        request.HasPeople = onlyPhotosWithPeople;
        request.Type = onlyPhotosWithPeople ? "IMAGE" : null;
        ApplyLocationFilter(request, locationFilter);
        var assets = await immich.SearchAssetsAllPagesAsync(immichSettings, request);
        var sourceLabel = rule.Type == AssetFilterConditionType.Album ? rule.Label : null;

        return ToAssetDictionary(assets, sourceLabel);
    }

    private static IEnumerable<string> FindAlbumIds(AssetFilterRule rule)
    {
        if (rule.Kind == AssetFilterRuleKind.Condition)
        {
            if (rule.Type == AssetFilterConditionType.Album && !string.IsNullOrEmpty(rule.Id))
            {
                yield return rule.Id;
            }
            yield break;
        }

        foreach (var child in rule.Children)
        {
            foreach (var id in FindAlbumIds(child))
            {
                yield return id;
            }
        }
    }

    private async Task<List<ImmichAsset>> SearchPeopleAssetsAsync(
        ImmichSettings settings, string? personId, IReadOnlyList<string> albumIds,
        DateFilter? dateFilter, LocationFilter? locationFilter, CancellationToken ct, bool onlyPhotosWithPeople = false)
    {
        var assets = new Dictionary<string, ImmichAsset>(StringComparer.Ordinal);
        // Immich's default search covers owned/partner assets. Album-only shares
        // require an explicit album scope, even when the person is shared too.
        // Query albums separately: an ID list means intersection, not union.
        foreach (var albumId in new string?[] { null }.Concat(albumIds))
        {
            ct.ThrowIfCancellationRequested();
            var request = new SearchAssetsRequest
            {
                PersonIds = personId == null ? null : [personId],
                HasPeople = onlyPhotosWithPeople,
                Type = onlyPhotosWithPeople ? "IMAGE" : null
            };
            if (albumId != null)
            {
                request.AlbumIds = [albumId];
            }
            if (dateFilter != null)
            {
                ApplyDateFilter(request, dateFilter);
            }
            ApplyLocationFilter(request, locationFilter);
            foreach (var asset in await immich.SearchAssetsAllPagesAsync(settings, request))
            {
                assets.TryAdd(asset.Id, asset);
            }
        }
        return assets.Values.ToList();
    }

    private async Task<Dictionary<string, CollectedAsset>> FetchMemoriesAssetsAsync(
        Profile profile,
        ImmichSettings immichSettings,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var today = MemoryDate.For(profile, _time.GetUtcNow());
        var capabilities = profile.Memories.IncludeBirthdays || profile.Memories.IncludeSaved
            ? await immich.GetSearchApiCapabilitiesAsync(immichSettings) : null;
        var includeBirthdays = profile.Memories.IncludeBirthdays && capabilities?.BirthdayMemories == true;
        var includeSaved = profile.Memories.IncludeSaved && capabilities?.SavedMemories == true;
        if (!profile.HasOnThisDaySource() && !includeBirthdays && !includeSaved)
        {
            return new(StringComparer.Ordinal);
        }
        var memories = profile.HasOnThisDaySource() || includeBirthdays
            ? await immich.GetMemoriesAsync(immichSettings, today) : new List<ImmichMemory>();
        var dailyIds = memories.Select(memory => memory.Id).ToHashSet(StringComparer.Ordinal);
        if (includeSaved)
        {
            memories.AddRange(await immich.GetSavedMemoriesAsync(immichSettings));
        }
        var results = new Dictionary<string, CollectedAsset>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var memory in memories)
        {
            var birthday = string.Equals(memory.Type, "birthday", StringComparison.Ordinal);
            var daily = dailyIds.Contains(memory.Id) && (birthday ? includeBirthdays : profile.HasOnThisDaySource() &&
                (memory.Type == "on_this_day" || string.IsNullOrEmpty(memory.Type)));
            // Empty types are tolerated for older servers; unknown future types stay opt-in.
            if ((!daily && !(includeSaved && memory.IsSaved)) ||
                (!string.IsNullOrEmpty(memory.Id) && !seen.Add(memory.Id)))
            {
                continue;
            }
            var caption = birthday && daily && profile.Memories.ShowBirthdayCaptions
                ? (string.IsNullOrWhiteSpace(memory.Data?.PersonName) ? "Birthday memories" : $"{memory.Data.PersonName.Trim()}'s birthday")
                : null;
            foreach (var asset in memory.Assets)
            {
                if (results.TryGetValue(asset.Id, out var existing))
                {
                    results[asset.Id] = existing with { BirthdayCaption = existing.BirthdayCaption ?? caption };
                }
                else
                {
                    results.Add(asset.Id, new(asset, null, caption));
                }
            }
        }

        return results;
    }

    private static Dictionary<string, CollectedAsset> Union(
        IEnumerable<Dictionary<string, CollectedAsset>> childResults)
    {
        var combined = new Dictionary<string, CollectedAsset>(StringComparer.Ordinal);

        foreach (var child in childResults)
        {
            foreach (var (id, asset) in child)
            {
                combined.TryAdd(id, asset);
            }
        }

        return combined;
    }

    private static Dictionary<string, CollectedAsset> Intersect(
        IReadOnlyList<Dictionary<string, CollectedAsset>> childResults)
    {
        var intersection = new Dictionary<string, CollectedAsset>(childResults[0], StringComparer.Ordinal);

        foreach (var key in intersection.Keys.ToList())
        {
            var matchingEntries = new List<CollectedAsset> { intersection[key] };
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
                : new CollectedAsset(matchingEntries[0].Asset, null);
        }

        return intersection;
    }

    private static Dictionary<string, CollectedAsset> ToAssetDictionary(
        IEnumerable<ImmichAsset> assets,
        string? sourceLabel)
    {
        var results = new Dictionary<string, CollectedAsset>(StringComparer.Ordinal);

        foreach (var asset in assets)
        {
            results.TryAdd(asset.Id, new(asset, sourceLabel));
        }

        return results;
    }

    private static void ApplyQualityFilter(
        Dictionary<string, CollectedAsset> assets,
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

    private SearchAssetsRequest BuildSearchRequest(AssetFilterRule rule, DateFilter? dateFilter)
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

    private static void ApplyLocationFilter(SearchAssetsRequest request, LocationFilter? location)
    {
        request.Country = CleanLocation(location?.Country);
        request.State = CleanLocation(location?.State);
        request.City = CleanLocation(location?.City);
    }

    private static string? CleanLocation(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesLocation(ImmichAsset asset, LocationFilter? location) =>
        MatchesField(asset.ExifInfo?.Country, location?.Country) &&
        MatchesField(asset.ExifInfo?.State, location?.State) &&
        MatchesField(asset.ExifInfo?.City, location?.City);

    private static bool MatchesField(string? actual, string? expected) =>
        CleanLocation(expected) is not { } value || string.Equals(actual, value, StringComparison.Ordinal);

    private void ApplyDateFilter(SearchAssetsRequest request, DateFilter filter)
    {
        if (filter.Type == "range")
        {
            request.TakenAfter = filter.From;
            request.TakenBefore = filter.To;
        }
        else if (filter.Type == "rolling" && filter.Amount.HasValue)
        {
            var now = _time.GetUtcNow();
            var from = filter.Unit switch
            {
                "days"   => now.AddDays(-filter.Amount.Value),
                "weeks"  => now.AddDays(-filter.Amount.Value * 7),
                "months" => now.AddMonths(-filter.Amount.Value),
                "years"  => now.AddYears(-filter.Amount.Value),
                _        => now
            };
            request.TakenAfter = from.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
