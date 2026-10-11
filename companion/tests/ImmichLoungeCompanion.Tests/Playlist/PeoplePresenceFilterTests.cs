using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class PeoplePresenceFilterTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "key" };
    private static IImmichClient SupportedClient()
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.2.0", true));
        return client;
    }
    private static List<ImmichAsset> Photos(params string[] ids) => ids.Select(id => new ImmichAsset { Id = id, Type = "IMAGE" }).ToList();
    private static AssetFilterRule Condition(AssetFilterConditionType type, string id) => new() { Kind = AssetFilterRuleKind.Condition, Type = type, Id = id, Label = id };

    [TestMethod]
    public async Task WholeLibraryFiltersByAnyFaceWithoutSelectingPersonIds()
    {
        var client = SupportedClient();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().HasPeople ? Photos("unnamed-face") : Photos("unnamed-face", "landscape"));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new() { OnlyPhotosWithPeople = true }, Settings);
        CollectionAssert.AreEquivalent(new[] { "unnamed-face" }, result.Keys.ToArray());
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r =>
            r.HasPeople && r.Type == "IMAGE" && r.PersonIds == null && r.AlbumIds == null));
    }

    [TestMethod]
    [DataRow(AssetFilterGroupOperator.And)]
    [DataRow(AssetFilterGroupOperator.Or)]
    public async Task NestedSourcesPreserveFacePredicateAndSharedAlbumScopes(AssetFilterGroupOperator op)
    {
        var client = SupportedClient();
        var requests = new List<SearchAssetsRequest>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>(); requests.Add(r);
            Assert.IsTrue(r.HasPeople);
            Assert.AreEqual("IMAGE", r.Type);
            Assert.AreEqual("2026-01-01", r.TakenAfter);
            Assert.AreEqual("Canada", r.Country);
            return r.PersonIds != null && r.AlbumIds == null ? [] : Photos("shared-face");
        });
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithPeople = true,
            DateFilter = new() { Type = "range", From = "2026-01-01" },
            LocationFilter = new() { Country = " Canada " },
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = op,
                Children = [Condition(AssetFilterConditionType.Album, "shared"), new()
                {
                    Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                    Children = [Condition(AssetFilterConditionType.Person, "person"), Condition(AssetFilterConditionType.Tag, "tag")]
                }]
            }
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "shared-face" }, result.Keys.ToArray());
        Assert.AreEqual(4, requests.Count);
        Assert.IsTrue(requests.Any(r => r.PersonIds != null && r.AlbumIds?.Single() == "shared"));
    }

    [TestMethod]
    public async Task MemoriesKeepFacesOutsideDateRangeAndApplyExclusionsAfterwards()
    {
        var client = SupportedClient();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.3.1", true));
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            if (r.PersonIds != null) { return Photos("excluded-memory"); }
            Assert.IsTrue(r.HasPeople);
            if (r.TakenAfter != null) { return Photos("source-face"); }
            return r.AlbumIds == null ? Photos("birthday-face", "excluded-memory") : Photos("shared-memory");
        });
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([
            new() { Type = "birthday", Data = new() { PersonName = "Alex" }, Assets = Photos("birthday-face") },
            new() { Type = "on_this_day", Assets = Photos("shared-memory", "landscape-memory", "excluded-memory") }
        ]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithPeople = true,
            ContentSources = [new() { Type = "album", Id = "shared", Label = "Shared" }, new() { Type = "memories" }],
            DateFilter = new() { Type = "range", From = "2026-01-01" },
            Memories = new() { IncludeBirthdays = true },
            ExcludedPeople = [new() { Type = "person", Id = "exclude" }]
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "source-face", "birthday-face", "shared-memory" }, result.Keys.ToArray());
        Assert.AreEqual("Alex's birthday", result["birthday-face"].BirthdayCaption);
        Assert.AreEqual("Shared", result["source-face"].SourceLabel);
    }

    [TestMethod]
    public async Task MemoriesOnlyNeverAddsOtherFaceMatchesFromLibrary()
    {
        var client = SupportedClient();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Photos("memory-face", "landscape") }]);
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(Photos("memory-face", "unrelated-face"));
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithPeople = true, ContentSources = [new() { Type = "memories" }]
        }, Settings);
        CollectionAssert.AreEquivalent(new[] { "memory-face" }, result.Keys.ToArray());
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(r => r.HasPeople && r.TakenAfter == null));
    }

    [TestMethod]
    public async Task UnsupportedServerRejectsEvenAnEmptyMemorySelection()
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Settings).Returns(new SearchApiCapabilities("3.1.0", false));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithPeople = true, ContentSources = [new() { Type = "memories" }]
        }, Settings));
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>());
        await client.DidNotReceive().GetMemoriesAsync(Settings, Arg.Any<DateOnly>());
    }

    [TestMethod]
    public async Task FailedSharedMemoryFaceQueryDoesNotServeUnfilteredMemories()
    {
        var client = SupportedClient();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = Photos("memory") }]);
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var r = call.Arg<SearchAssetsRequest>();
            if (r.HasPeople && r.AlbumIds != null && r.TakenAfter == null) { throw new HttpRequestException("Permission denied"); }
            return Photos("memory");
        });
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(new()
        {
            OnlyPhotosWithPeople = true,
            ContentSources = [new() { Type = "album", Id = "shared" }, new() { Type = "memories" }],
            DateFilter = new() { Type = "range", From = "2026-01-01" }
        }, Settings));
    }
}
