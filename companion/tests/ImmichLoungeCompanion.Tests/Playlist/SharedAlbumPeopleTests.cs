using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class SharedAlbumPeopleTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "key" };
    private static AssetFilterRule Album(string id) => new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = id };
    private static AssetFilterRule Person(string id) => new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = id };
    private static AssetFilterRule Group(AssetFilterGroupOperator op, params AssetFilterRule[] children) => new() { Kind = AssetFilterRuleKind.Group, Operator = op, Children = [.. children] };
    private static List<ImmichAsset> Assets(params string[] ids) => ids.Select(id => new ImmichAsset { Id = id }).ToList();

    [TestMethod]
    public async Task NestedAlbumUnionAndPersonIncludesSharedMatchesWithoutDuplicates()
    {
        var client = Substitute.For<IImmichClient>();
        var requests = new List<SearchAssetsRequest>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>(); requests.Add(r);
            var album = r.AlbumIds?.SingleOrDefault();
            return r.PersonIds == null ? Assets("shared", "no-person")
                : album == null ? Assets("own") : Assets("shared", "shared");
        });
        var profile = new Profile
        {
            AssetFilter = Group(AssetFilterGroupOperator.And,
                Group(AssetFilterGroupOperator.Or, Album("a"), Album("b"), Album("a")), Person("p")),
            DateFilter = new() { Type = "range", From = "2020-01-01", To = "2021-01-01" },
            LocationFilter = new() { Country = " Canada " }
        };
        var result = await new PlaylistAssetCollector(client).CollectAsync(profile, Settings);
        CollectionAssert.AreEquivalent(new[] { "shared" }, result.Keys.ToArray());
        var peopleQueries = requests.Where(r => r.PersonIds != null).ToArray();
        Assert.AreEqual(3, peopleQueries.Length, "Default scope plus each distinct album separately.");
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, peopleQueries.Where(r => r.AlbumIds != null).Select(r => r.AlbumIds!.Single()).ToArray());
        foreach (var r in peopleQueries)
        {
            Assert.AreEqual("2020-01-01", r.TakenAfter);
            Assert.AreEqual("2021-01-01", r.TakenBefore);
            Assert.AreEqual("Canada", r.Country);
        }
    }

    [TestMethod]
    public async Task AlbumOrPersonRetainsOwnedMatchesAndUnmatchedAlbumPhotos()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            return r.PersonIds == null ? Assets("shared-person", "shared-other")
                : r.AlbumIds == null ? Assets("own-person") : Assets("shared-person");
        });
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            AssetFilter = Group(AssetFilterGroupOperator.Or, Person("p"), Album("a"))
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "shared-person", "shared-other", "own-person" }, result.Keys.ToArray());
    }

    [TestMethod]
    public async Task ExclusionsSearchSelectedAlbumsAndMemoriesWithoutDateOrLocationRestrictions()
    {
        var client = Substitute.For<IImmichClient>();
        var exclusions = new List<SearchAssetsRequest>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            if (r.PersonIds == null) { return Assets("shared-excluded", "safe"); }
            exclusions.Add(r);
            return r.AlbumIds == null ? Assets("old-memory") : Assets("shared-excluded", "old-shared-memory");
        });
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Assets("old-memory", "old-shared-memory") }]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            ContentSources = [new() { Type = "album", Id = "a" }, new() { Type = "memories" }],
            DateFilter = new() { Type = "range", From = "2026-01-01" },
            ExcludedPeople = [new() { Type = "person", Id = "p" }, new() { Type = "person", Id = "p" }]
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "safe" }, result.Keys.ToArray());
        Assert.AreEqual(2, exclusions.Count);
        foreach (var r in exclusions)
        {
            Assert.IsNull(r.TakenAfter);
            Assert.IsNull(r.TakenBefore);
            Assert.IsNull(r.Country);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedAlbumScopedPersonSearchDoesNotServePartialResults(bool exclusion)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            return r.AlbumIds != null && r.PersonIds != null
                ? Task.FromException<List<ImmichAsset>>(new HttpRequestException("Album scope denied"))
                : Task.FromResult(Assets("photo"));
        });
        var profile = new Profile { AssetFilter = Group(AssetFilterGroupOperator.And, Album("a"), Person("p")) };
        if (exclusion)
        {
            profile.AssetFilter = Album("a");
            profile.ExcludedPeople = [new() { Type = "person", Id = "p" }];
        }
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(profile, Settings));
    }

    [TestMethod]
    public async Task EmptyAlbumIntersectionStaysEmptyAndPersonOnlyDoesNotDiscoverOtherAlbums()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().PersonIds == null ? [] : Assets("own"));
        var collector = new PlaylistAssetCollector(client);
        var empty = await collector.CollectAsync(new() { AssetFilter = Group(AssetFilterGroupOperator.And, Album("empty"), Person("p")) }, Settings);
        Assert.AreEqual(0, empty.Count);
        client.ClearReceivedCalls();
        var own = await collector.CollectAsync(new() { AssetFilter = Person("p") }, Settings);
        CollectionAssert.AreEqual(new[] { "own" }, own.Keys.ToArray());
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r => r.AlbumIds == null));
        await client.DidNotReceive().GetAlbumsAsync(Arg.Any<ImmichSettings>());
    }
}
