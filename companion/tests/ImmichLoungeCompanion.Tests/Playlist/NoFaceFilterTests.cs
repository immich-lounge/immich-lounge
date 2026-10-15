using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class NoFaceFilterTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "key" };
    private static List<ImmichAsset> Photos(params string[] ids) => ids.Select(id => new ImmichAsset
        { Id = id, Type = "IMAGE", IsFavorite = true, ExifInfo = new() { Rating = 5 } }).ToList();
    private static IImmichClient Client()
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.2.0", true));
        return client;
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("all")]
    public async Task WholeLibraryUsesServerPredicateInsteadOfEmptyPeopleMetadata(string? source)
    {
        var client = Client();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().HasNoPeople ? Photos("scenery") : Photos("scenery", "unnamed-face", "named-face"));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new() { PhotoSource = source, OnlyPhotosWithoutPeople = true }, Settings);
        CollectionAssert.AreEquivalent(new[] { "scenery" }, result.Keys.ToArray());
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r => r.HasNoPeople && !r.HasPeople && r.Type == "IMAGE" && r.PersonIds == null));
    }

    [TestMethod]
    [DataRow(AssetFilterGroupOperator.And)]
    [DataRow(AssetFilterGroupOperator.Or)]
    public async Task NestedSourcesAndSharedAlbumQueriesKeepNoFacePredicate(AssetFilterGroupOperator op)
    {
        var client = Client();
        var requests = new List<SearchAssetsRequest>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var request = call.Arg<SearchAssetsRequest>(); requests.Add(request);
            Assert.IsTrue(request.HasNoPeople); Assert.IsFalse(request.HasPeople); Assert.AreEqual("IMAGE", request.Type);
            Assert.AreEqual("Canada", request.Country); Assert.AreEqual("2020-01-01", request.TakenAfter);
            return request.PersonIds != null && request.AlbumIds == null ? [] : Photos("scenery");
        });
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "selected", OnlyPhotosWithoutPeople = true, OnlyFavorites = true, MinimumRating = 4,
            DateFilter = new() { Type = "range", From = "2020-01-01" }, LocationFilter = new() { Country = "Canada" },
            AssetFilter = new() { Kind = AssetFilterRuleKind.Group, Operator = op, Children = [
                new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "shared" },
                new() { Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or, Children = [
                    new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = "person" },
                    new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Tag, Id = "tag" }
                ] }
            ] }
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "scenery" }, result.Keys.ToArray());
        Assert.IsTrue(requests.Any(r => r.PersonIds != null && r.AlbumIds?.Single() == "shared"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("memories")]
    public async Task MemoriesIntersectActualNoFaceMatchesWithoutAddingUnrelatedPhotos(string? source)
    {
        var client = Client();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Photos("scenery", "face") }]);
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(Photos("scenery", "unrelated"));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = source, OnlyPhotosWithoutPeople = true, ContentSources = [new() { Type = "memories" }]
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "scenery" }, result.Keys.ToArray()); Assert.IsTrue(result["scenery"].IsMemory);
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r => r.HasNoPeople && r.TakenAfter == null));
    }

    [TestMethod]
    public async Task SharedAdditiveMemoriesUseAlbumScopeAndKeepExclusionsIndependent()
    {
        var client = Client();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Photos("shared-scenery", "face", "excluded") }]);
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            if (r.PersonIds != null) { Assert.IsFalse(r.HasNoPeople); return Photos("excluded"); }
            Assert.IsTrue(r.HasNoPeople);
            return r.TakenAfter != null ? Photos("regular") : r.AlbumIds != null ? Photos("shared-scenery", "excluded") : [];
        });
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithoutPeople = true,
            ContentSources = [new() { Type = "album", Id = "shared", Label = "Shared" }, new() { Type = "memories" }],
            ExcludedPeople = [new() { Type = "person", Id = "exclude" }], DateFilter = new() { Type = "range", From = "2025-01-01" }
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "regular", "shared-scenery" }, result.Keys.ToArray());
        Assert.AreEqual("Shared", result["regular"].SourceLabel);
    }

    [TestMethod]
    [DataRow("3.1.0")]
    [DataRow(null)]
    public async Task UnsupportedOrUnknownServerNeverServesUnfilteredPhotos(string? version)
    {
        var client = Client();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities(version, false));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => new PlaylistAssetCollector(client).CollectAsync(new() { OnlyPhotosWithoutPeople = true }, Settings));
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>());
    }

    [TestMethod]
    public async Task MemoryPredicateFailureDoesNotFallBackToUnfilteredMemories()
    {
        var client = Client();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Photos("memory") }]);
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns<List<ImmichAsset>>(_ => throw new HttpRequestException("Denied"));
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(new()
            { OnlyPhotosWithoutPeople = true, ContentSources = [new() { Type = "memories" }] }, Settings));
    }
}
