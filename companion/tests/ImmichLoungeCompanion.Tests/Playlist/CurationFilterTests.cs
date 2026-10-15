using System.Net;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class CurationFilterTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "key" };
    private static ImmichAsset Photo(string id, bool? favorite, int? rating) => new()
    {
        Id = id, Type = "IMAGE", IsFavorite = favorite,
        ExifInfo = new() { Rating = rating, Country = "Canada", FileSizeInByte = 2048 }
    };
    private static List<ImmichAsset> Selection() =>
        [Photo("five", true, 5), Photo("four", true, 4), Photo("three", true, 3),
         Photo("not-favorite", false, 5), Photo("unrated", true, null), Photo("rejected", false, -1)];

    [TestMethod]
    [DataRow(false, null, "five,four,three,not-favorite,unrated,rejected")]
    [DataRow(true, null, "five,four,three,unrated")]
    [DataRow(false, 4, "five,four,not-favorite")]
    [DataRow(true, 4, "five,four")]
    [DataRow(true, 5, "five")]
    [DataRow(false, 1, "five,four,three,not-favorite")]
    public async Task FiltersUseInclusiveMinimumAndRequireBothPredicates(bool favorites, int? minimum, string expected)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(Selection());
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "all", OnlyFavorites = favorites, MinimumRating = minimum
        }, Settings);
        CollectionAssert.AreEquivalent(expected.Split(','), result.Keys.ToArray());
        await client.DidNotReceive().GetAssetAsync(Settings, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow("album", null)]
    [DataRow("album", "selected")]
    [DataRow("person", "selected")]
    [DataRow("tag", "selected")]
    public async Task SelectedSourcesKeepTheirScopeAndAlbumLabels(string type, string? source)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(Selection());
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = source, ContentSources = [new() { Type = type, Id = "selected", Label = "Family" }],
            OnlyFavorites = true, MinimumRating = 4
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "five", "four" }, result.Keys.ToArray());
        Assert.AreEqual(type == "album" ? "Family" : null, result["five"].SourceLabel);
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r =>
            type == "album" ? r.AlbumIds != null && r.AlbumIds.Contains("selected") :
            type == "person" ? r.PersonIds != null && r.PersonIds.Contains("selected") :
            r.TagIds != null && r.TagIds.Contains("selected")));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("memories")]
    public async Task AbbreviatedMemoriesAreHydratedAndKeepMemoryContext(string? source)
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.3.1", true));
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([
            new() { Type = "on_this_day", Assets = [new() { Id = "five", Type = "IMAGE" }, new() { Id = "three", Type = "IMAGE" }] },
            new() { Type = "birthday", Data = new() { PersonName = "Alex" }, Assets = [new() { Id = "five", Type = "IMAGE" }] }
        ]);
        client.GetAssetAsync(Settings, "five", Arg.Any<CancellationToken>()).Returns(Photo("five", true, 5));
        client.GetAssetAsync(Settings, "three", Arg.Any<CancellationToken>()).Returns(Photo("three", true, 3));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = source, ContentSources = [new() { Type = "memories" }],
            Memories = new() { IncludeBirthdays = true }, OnlyFavorites = true, MinimumRating = 4
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "five" }, result.Keys.ToArray());
        Assert.IsTrue(result["five"].IsMemory);
        Assert.AreEqual("Alex's birthday", result["five"].BirthdayCaption);
        await client.Received(1).GetAssetAsync(Settings, "five", Arg.Any<CancellationToken>());
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>());
    }

    [TestMethod]
    public async Task LegacyAdditiveAndSavedMemoriesCannotBypassFilters()
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.3.1", true));
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([Photo("source", true, 4)]);
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([
            new() { Assets = [Photo("daily", false, 5), Photo("good-daily", true, 4)] }
        ]);
        client.GetSavedMemoriesAsync(Settings).Returns([
            new() { Id = "saved", IsSaved = true, Assets = [Photo("saved", true, 2), Photo("good-saved", true, 5)] }
        ]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            ContentSources = [new() { Type = "album", Id = "album" }, new() { Type = "memories" }],
            Memories = new() { IncludeSaved = true }, OnlyFavorites = true, MinimumRating = 4
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "source", "good-daily", "good-saved" }, result.Keys.ToArray());
    }

    [TestMethod]
    public async Task HydratedMetadataStillPassesThroughQualityFilter()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([new() { Id = "photo", Type = "IMAGE" }]);
        client.GetAssetAsync(Settings, "photo", Arg.Any<CancellationToken>()).Returns(Photo("photo", true, 5));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyFavorites = true, MinimumRating = 4, Quality = new() { MinFileSizeKb = 3 }
        }, Settings);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task MissingAssetsOrFavoriteMetadataDoNotMatch()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([
            new() { Id = "deleted", Type = "IMAGE" }, Photo("sanitized", null, 5)
        ]);
        client.GetAssetAsync(Settings, "sanitized", Arg.Any<CancellationToken>()).Returns(Photo("sanitized", null, 5));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new() { OnlyFavorites = true }, Settings);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task HydrationPermissionFailureDoesNotServeUnfilteredSelection()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([new() { Id = "denied", Type = "IMAGE" }]);
        client.GetAssetAsync(Settings, "denied", Arg.Any<CancellationToken>())
            .Returns<ImmichAsset?>(_ => throw new HttpRequestException("Denied", null, HttpStatusCode.Forbidden));
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => new PlaylistAssetCollector(client)
            .CollectAsync(new() { OnlyFavorites = true }, Settings));
    }

    [TestMethod]
    public async Task DisabledFiltersDoNotHydrateMissingMetadata()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([new() { Id = "photo", Type = "IMAGE" }]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new(), Settings);
        Assert.AreEqual(1, result.Count);
        await client.DidNotReceive().GetAssetAsync(Settings, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RatingOnlyHydratesMissingExifAndSkipsStillUnratedAssets()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([
            new() { Id = "rated", Type = "IMAGE", IsFavorite = false },
            new() { Id = "no-exif", Type = "IMAGE" }, Photo("unrated", false, null)
        ]);
        client.GetAssetAsync(Settings, "rated", Arg.Any<CancellationToken>()).Returns(Photo("rated", false, 4));
        client.GetAssetAsync(Settings, "no-exif", Arg.Any<CancellationToken>()).Returns(new ImmichAsset { Id = "no-exif", Type = "IMAGE" });
        var result = await new PlaylistAssetCollector(client).CollectAsync(new() { MinimumRating = 4 }, Settings);
        CollectionAssert.AreEquivalent(new[] { "rated" }, result.Keys.ToArray());
        await client.DidNotReceive().GetAssetAsync(Settings, "unrated", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(AssetFilterGroupOperator.And, "four")]
    [DataRow(AssetFilterGroupOperator.Or, "five,four")]
    public async Task CurationPreservesNestedSourceMatching(AssetFilterGroupOperator op, string expected)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().AlbumIds != null ? [Photo("five", true, 5), Photo("four", true, 4)] :
            [Photo("four", true, 4), Photo("low", true, 3)]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "selected", OnlyFavorites = true, MinimumRating = 4,
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = op,
                Children = [
                    new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "album" },
                    new() { Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                        Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Tag, Id = "tag" }] }
                ]
            }
        }, Settings);
        CollectionAssert.AreEquivalent(expected.Split(','), result.Keys.ToArray());
    }
}
