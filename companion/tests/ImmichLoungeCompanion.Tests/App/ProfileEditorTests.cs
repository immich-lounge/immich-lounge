using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ImmichLoungeCompanion.Components.Pages;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class ProfileEditorTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public void Dispose() => _factory.Dispose();

    [TestMethod]
    [DataRow("album")]
    [DataRow("person")]
    [DataRow("tag")]
    public async Task ChangedSources_AffectPreviewAndSavedFilter(string sourceType)
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "source-edit",
            Name = "Source edit",
            ContentSources = [new() { Type = sourceType, Id = "old-source", Label = "Old" }]
        });

        // SourcePicker mutates the existing list before invoking SelectedChanged.
        profile.ContentSources.Clear();
        profile.ContentSources.Add(new() { Type = sourceType, Id = "new-source", Label = "New" });
        var payload = BuildPayload(editor);
        Assert.AreEqual("new-source", payload.AssetFilter!.Children.Single().Id);
        Assert.AreEqual("New", payload.AssetFilter.Children.Single().Label);
        Assert.AreEqual("old-source", profile.AssetFilter!.Children.Single().Id,
            "Preparing a preview must not mutate the editor's loaded filter.");

        SearchAssetsRequest? request = null;
        _factory.ImmichClient.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(call =>
            {
                request = call.Arg<SearchAssetsRequest>();
                return [new ImmichAsset { Id = "new-photo", Type = "IMAGE" }];
            });
        var preview = await client.PostAsJsonAsync("/api/profiles/preview-count", payload);
        Assert.AreEqual(HttpStatusCode.OK, preview.StatusCode);
        Assert.IsNotNull(request);
        var ids = sourceType switch
        {
            "album" => request.AlbumIds,
            "person" => request.PersonIds,
            _ => request.TagIds
        };
        CollectionAssert.AreEqual(new[] { "new-source" }, ids!.ToArray());

        var saved = await client.PutAsJsonAsync("/api/profiles/source-edit", payload);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        var reloaded = await client.GetFromJsonAsync<Profile>("/api/profiles/source-edit");
        Assert.AreEqual("new-source", reloaded!.AssetFilter!.Children.Single().Id);
    }

    [TestMethod]
    public async Task UnchangedFlatRules_PreserveMissingLabelsAndMemories()
    {
        using var client = _factory.CreateClient();
        var original = new Profile
        {
            Id = "unchanged-flat", Name = "Unchanged flat rules",
            ContentSources = [new() { Type = "Memories", Id = "memories" }],
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "album-1" }]
            }
        };
        var (editor, profile) = await LoadEditorAsync(client, original);
        profile.Slideshow.IntervalSeconds = 25;
        var payload = BuildPayload(editor);
        Assert.AreEqual(JsonSerializer.Serialize(original.AssetFilter), JsonSerializer.Serialize(payload.AssetFilter));
        Assert.AreEqual(1, payload.ContentSources.Count(source => source.Type.Equals("memories", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task AddingFirstSource_KeepsMemoriesAndBuildsFilter()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "first-source", Name = "First source",
            ContentSources = [new() { Type = "memories", Id = "memories", Label = "Memories" }]
        });
        profile.ContentSources.Add(new() { Type = "album", Id = "new-album", Label = "New" });
        var payload = BuildPayload(editor);
        Assert.AreEqual("new-album", payload.AssetFilter!.Children.Single().Id);
        Assert.AreEqual(1, payload.ContentSources.Count(source => source.Type == "memories"));
        var saved = await client.PutAsJsonAsync("/api/profiles/first-source", payload);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
    }

    [TestMethod]
    public async Task RemovingAllSources_ClearsGeneratedFilter()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "remove-sources", Name = "Remove sources",
            ContentSources = [new() { Type = "album", Id = "old-album" }]
        });
        profile.ContentSources.Clear();
        var payload = BuildPayload(editor);
        Assert.IsNull(payload.AssetFilter);
        var saved = await client.PutAsJsonAsync("/api/profiles/remove-sources", payload);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        Assert.IsNull((await client.GetFromJsonAsync<Profile>("/api/profiles/remove-sources"))!.AssetFilter);
    }

    [TestMethod]
    public async Task CustomRules_SurviveOtherSettingChanges()
    {
        using var client = _factory.CreateClient();
        var custom = new Profile
        {
            Id = "custom-rules", Name = "Custom rules",
            ContentSources = [new() { Type = "album", Id = "album-1" }],
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.And,
                Children =
                [
                    new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "album-1" },
                    new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = "person-1" }
                ]
            }
        };
        var (editor, profile) = await LoadEditorAsync(client, custom);
        Assert.IsFalse((bool)GetField(editor, "_canEditContentSources")!);
        profile.Slideshow.IntervalSeconds = 25;
        var payload = BuildPayload(editor);
        Assert.AreEqual(JsonSerializer.Serialize(custom.AssetFilter), JsonSerializer.Serialize(payload.AssetFilter));
        var saved = await client.PutAsJsonAsync("/api/profiles/custom-rules", payload);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        var reloaded = await client.GetFromJsonAsync<Profile>("/api/profiles/custom-rules");
        Assert.AreEqual(25, reloaded!.Slideshow.IntervalSeconds);
        Assert.AreEqual(JsonSerializer.Serialize(custom.AssetFilter), JsonSerializer.Serialize(reloaded.AssetFilter));
    }

    [TestMethod]
    public void ContentSourceEditing_OnlyAllowsRepresentableRules()
    {
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a" }, new() { Type = "person", Id = "p" }]
        };
        Assert.IsTrue(profile.CanEditContentSources());
        profile.NormalizeAssetFilter();
        profile.AssetFilter!.Children.Reverse();
        profile.AssetFilter.Children[0].Label = "Renamed";
        Assert.IsTrue(profile.CanEditContentSources(), "Ordering and labels do not change the source selection.");
        profile.AssetFilter.Operator = AssetFilterGroupOperator.And;
        Assert.IsFalse(profile.CanEditContentSources());
        profile.AssetFilter.Operator = AssetFilterGroupOperator.Or;
        profile.AssetFilter.Children[0].Id = "custom-person";
        Assert.IsTrue(profile.CanEditContentSources(), "Flat OR rules can be edited even with a stale source list.");
        profile.AssetFilter.Children[0] = new AssetFilterRule
        {
            Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
            Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = "p" }]
        };
        Assert.IsFalse(profile.CanEditContentSources());
    }

    [TestMethod]
    public async Task StaleSourceList_LoadsEffectiveSelectionAndCanBeCorrected()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "stale-sources", Name = "Stale sources",
            ContentSources = [new() { Type = "album", Id = "new-album", Label = "New" }],
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "old-album", Label = "Old" }]
            }
        });
        Assert.IsTrue((bool)GetField(editor, "_canEditContentSources")!);
        Assert.AreEqual("old-album", profile.ContentSources.Single().Id);
        profile.ContentSources.Clear();
        profile.ContentSources.Add(new() { Type = "album", Id = "new-album", Label = "New" });
        var payload = BuildPayload(editor);
        var saved = await client.PutAsJsonAsync("/api/profiles/stale-sources", payload);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        var reloaded = await client.GetFromJsonAsync<Profile>("/api/profiles/stale-sources");
        Assert.AreEqual("new-album", reloaded!.ContentSources.Single().Id);
        Assert.AreEqual("new-album", reloaded.AssetFilter!.Children.Single().Id);
    }

    private static async Task<(ProfileEditor Editor, Profile Profile)> LoadEditorAsync(HttpClient client, Profile profile)
    {
        var created = await client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("local").Returns(client);
        var editor = new ProfileEditor();
        typeof(ProfileEditor).GetProperty("Id")!.SetValue(editor, profile.Id);
        typeof(ProfileEditor).GetProperty("HttpFactory", PrivateInstance)!.SetValue(editor, httpFactory);
        // Exercise the component's real load and request preparation without a browser or renderer.
        await (Task)typeof(ProfileEditor).GetMethod("LoadProfileAsync", PrivateInstance)!.Invoke(editor, null)!;
        return (editor, (Profile)GetField(editor, "_profile")!);
    }

    private static object? GetField(ProfileEditor editor, string name) =>
        typeof(ProfileEditor).GetField(name, PrivateInstance)!.GetValue(editor);

    private static Profile BuildPayload(ProfileEditor editor) =>
        (Profile)typeof(ProfileEditor).GetMethod("BuildProfilePayloadForRequest", PrivateInstance)!.Invoke(editor, null)!;
}
