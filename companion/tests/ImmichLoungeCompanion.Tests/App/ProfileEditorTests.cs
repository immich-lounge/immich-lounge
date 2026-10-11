using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ImmichLoungeCompanion.Components.Pages;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using NSubstitute;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class ProfileEditorTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public void Dispose() => _factory.Dispose();

    [TestMethod]
    public async Task ExplicitPhotoSource_SurvivesEditorPreviewAndSave()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "source-mode", Name = "Source mode", PhotoSource = "all",
            ContentSources = [new() { Type = "memories" }], Memories = new() { Prioritize = true }
        });
        Assert.AreEqual("all", profile.PhotoSource);
        var initialPreview = (string)typeof(ProfileEditor).GetMethod("BuildPreviewSnapshot", PrivateInstance)!.Invoke(editor, null)!;
        typeof(ProfileEditor).GetMethod("OnPhotoSourceChanged", PrivateInstance)!.Invoke(editor,
            [new ChangeEventArgs { Value = "memories" }]);
        Assert.AreEqual("memories", BuildPayload(editor).PhotoSource);
        Assert.AreNotEqual(initialPreview, (string)typeof(ProfileEditor).GetMethod("BuildPreviewSnapshot", PrivateInstance)!.Invoke(editor, null)!);
        (await client.PutAsJsonAsync("/api/profiles/source-mode", BuildPayload(editor))).EnsureSuccessStatusCode();
        var reloaded = (await client.GetFromJsonAsync<Profile>("/api/profiles/source-mode"))!;
        Assert.AreEqual("memories", reloaded.PhotoSource);
        Assert.IsTrue(reloaded.Memories.Prioritize);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PeoplePresenceSettingSurvivesSaveReloadAndChangesPreview(bool supported)
    {
        _factory.ImmichClient.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>())
            .Returns(new SearchApiCapabilities(supported ? "3.2.0" : "3.1.0", supported));
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile { Id = "people-presence", Name = "People" });
        Assert.IsFalse(profile.OnlyPhotosWithPeople, "Existing profiles remain unfiltered.");
        Assert.AreEqual(supported, (bool)GetField(editor, "_peopleFilterAvailable")!);
        var snapshotMethod = typeof(ProfileEditor).GetMethod("BuildPreviewSnapshot", PrivateInstance)!;
        var before = (string)snapshotMethod.Invoke(editor, null)!;
        profile.OnlyPhotosWithPeople = true;
        Assert.AreNotEqual(before, (string)snapshotMethod.Invoke(editor, null)!);
        var payload = BuildPayload(editor);
        Assert.IsTrue(payload.OnlyPhotosWithPeople);
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/people-presence", payload)).StatusCode);
        var (reloadedEditor, reloaded) = await ReloadEditorAsync(client, "people-presence");
        Assert.IsTrue(reloaded.OnlyPhotosWithPeople, "An unavailable filter must remain saved until explicitly disabled.");
        if (!supported)
        {
            var preview = await client.PostAsJsonAsync("/api/profiles/preview-count", BuildPayload(reloadedEditor));
            Assert.AreEqual(HttpStatusCode.BadRequest, preview.StatusCode);
            StringAssert.Contains(await preview.Content.ReadAsStringAsync(), "Immich 3.2");
        }
        reloaded.OnlyPhotosWithPeople = false;
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/people-presence", BuildPayload(reloadedEditor))).StatusCode);
        Assert.IsFalse((await client.GetFromJsonAsync<Profile>("/api/profiles/people-presence"))!.OnlyPhotosWithPeople);
    }

    [TestMethod]
    public async Task BirthdaySettings_SurviveEditorSaveAndReloadWithoutEnablingOnThisDay()
    {
        _factory.ImmichClient.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>())
            .Returns(new SearchApiCapabilities("3.3.1", true));
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "birthday-profile", Name = "Birthdays",
            Memories = new() { IncludeBirthdays = true, ShowBirthdayCaptions = false, TimeZoneId = "Europe/Zurich" }
        });
        Assert.IsFalse((bool)GetField(editor, "_memoriesEnabled")!);
        Assert.IsTrue((bool)GetField(editor, "_birthdayMemoriesAvailable")!);
        profile.Memories.ShowBirthdayCaptions = true;
        var saved = await client.PutAsJsonAsync("/api/profiles/birthday-profile", BuildPayload(editor));
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        var reloaded = await client.GetFromJsonAsync<Profile>("/api/profiles/birthday-profile");
        Assert.IsTrue(reloaded!.Memories.IncludeBirthdays);
        Assert.IsTrue(reloaded.Memories.ShowBirthdayCaptions);
        Assert.AreEqual("Europe/Zurich", reloaded.Memories.TimeZoneId);
        Assert.IsFalse(reloaded.HasOnThisDaySource());
    }

    [TestMethod]
    public async Task InvalidMemoryTimeZone_IsRejectedBySaveAndPreview()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile { Id = "invalid-zone", Name = "Invalid zone" });
        profile.Memories.TimeZoneId = "Not/A_Time_Zone";
        var payload = BuildPayload(editor);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profiles/invalid-zone", payload)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profiles/preview-count", payload)).StatusCode);
    }

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
    [DataRow(AssetFilterGroupOperator.Or)]
    [DataRow(AssetFilterGroupOperator.And)]
    public async Task UnchangedFlatRules_PreserveMissingLabelsAndMemories(AssetFilterGroupOperator matchOperator)
    {
        using var client = _factory.CreateClient();
        var original = new Profile
        {
            Id = "unchanged-flat", Name = "Unchanged flat rules",
            ContentSources = [new() { Type = "Memories", Id = "memories" }],
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group, Operator = matchOperator,
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
                    new() { Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                        Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = "person-1" }] }
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
        Assert.IsTrue(profile.CanEditContentSources(), "The match control can represent a flat AND group.");
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

    [TestMethod]
    public async Task MatchAllAndExclusions_AffectPreviewAndSurviveSave()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "people-rules", Name = "People rules",
            ContentSources = [new() { Type = "person", Id = "child" }, new() { Type = "album", Id = "family" }]
        });
        typeof(ProfileEditor).GetField("_sourceMatchOperator", PrivateInstance)!.SetValue(editor, AssetFilterGroupOperator.And);
        profile.ExcludedPeople.Add(new() { Type = "person", Id = "parent", Label = "Parent" });
        _factory.ImmichClient.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(call => call.Arg<SearchAssetsRequest>() switch
            {
                { PersonIds: ["parent"] } => [new ImmichAsset { Id = "with-parent", Type = "IMAGE" }],
                { PersonIds: ["child"] } => [new ImmichAsset { Id = "child-only", Type = "IMAGE" }, new ImmichAsset { Id = "with-parent", Type = "IMAGE" }, new ImmichAsset { Id = "outside-album", Type = "IMAGE" }],
                _ => [new ImmichAsset { Id = "child-only", Type = "IMAGE" }, new ImmichAsset { Id = "with-parent", Type = "IMAGE" }]
            });
        var payload = BuildPayload(editor);
        Assert.AreEqual(AssetFilterGroupOperator.And, payload.AssetFilter!.Operator);
        var preview = await client.PostAsJsonAsync("/api/profiles/preview-count", payload);
        Assert.AreEqual(HttpStatusCode.OK, preview.StatusCode);
        var result = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(1, result.GetProperty("matchedAssets").GetInt32());
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/people-rules", payload)).StatusCode);
        var (reloadedEditor, reloaded) = await ReloadEditorAsync(client, profile.Id);
        Assert.AreEqual(AssetFilterGroupOperator.And, GetField(reloadedEditor, "_sourceMatchOperator"));
        Assert.AreEqual("parent", reloaded.ExcludedPeople.Single().Id);
        reloaded.ContentSources.Add(new() { Type = "tag", Id = "holiday" });
        Assert.AreEqual(AssetFilterGroupOperator.And, BuildPayload(reloadedEditor).AssetFilter!.Operator);
    }

    [TestMethod]
    public async Task Exclusions_CanBeRemovedWithoutChangingSourceRules()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile
        {
            Id = "remove-exclusion", Name = "Remove exclusion",
            ContentSources = [new() { Type = "person", Id = "child" }],
            ExcludedPeople = [new() { Type = "person", Id = "parent" }]
        });
        profile.ExcludedPeople.Clear();
        var payload = BuildPayload(editor);
        Assert.AreEqual(0, payload.ExcludedPeople.Count);
        Assert.AreEqual("child", payload.AssetFilter!.Children.Single().Id);
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/remove-exclusion", payload)).StatusCode);
        Assert.AreEqual(0, (await client.GetFromJsonAsync<Profile>("/api/profiles/remove-exclusion"))!.ExcludedPeople.Count);
    }

    [TestMethod]
    public async Task TransitionDuration_EditorSavesDecimalInput()
    {
        using var client = _factory.CreateClient();
        var (editor, _) = await LoadEditorAsync(client, new Profile { Id = "editor-duration", Name = "Duration" });
        typeof(ProfileEditor).GetMethod("OnTransitionDurationChanged", PrivateInstance)!.Invoke(editor,
            [new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "1.7" }]);
        var payload = BuildPayload(editor);
        Assert.AreEqual(1.7, payload.Slideshow.TransitionDurationSeconds);
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/editor-duration", payload)).StatusCode);
        Assert.AreEqual(1.7, (await client.GetFromJsonAsync<Profile>("/api/profiles/editor-duration"))!.Slideshow.TransitionDurationSeconds);
    }

    [TestMethod]
    [DataRow("network")]
    [DataRow("timeout")]
    [DataRow("html")]
    public async Task FailedSave_KeepsEditsAndAllowsRetry(string failure)
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile { Id = "failed-save", Name = "Original" });
        profile.Name = "Unsaved name";
        using var failingClient = new HttpClient(new FailureHandler(failure)) { BaseAddress = new Uri("http://test") };
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("local").Returns(failingClient);
        typeof(ProfileEditor).GetProperty("HttpFactory", PrivateInstance)!.SetValue(editor, httpFactory);
        await (Task)typeof(ProfileEditor).GetMethod("SaveAsync", PrivateInstance)!.Invoke(editor, null)!;
        Assert.AreSame(profile, GetField(editor, "_profile"));
        Assert.AreEqual("Unsaved name", profile.Name);
        Assert.IsFalse((bool)GetField(editor, "_saving")!);
        Assert.IsNotNull(GetField(editor, "_saveError"));
        Assert.AreEqual("Original", (await client.GetFromJsonAsync<Profile>("/api/profiles/failed-save"))!.Name);
    }

    [TestMethod]
    public async Task CriteriaChangedWhilePreviewPending_IgnoresOldResponse()
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile { Id = "pending-preview", Name = "Preview" });
        using var handler = new PendingPreviewHandler();
        using var pendingClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("local").Returns(pendingClient);
        typeof(ProfileEditor).GetProperty("HttpFactory", PrivateInstance)!.SetValue(editor, httpFactory);
        var preview = (Task)typeof(ProfileEditor).GetMethod("PreviewMatchCountAsync", PrivateInstance)!.Invoke(editor, null)!;
        await handler.Started.Task;
        profile.ContentSources.Add(new() { Type = "album", Id = "different" });
        typeof(ProfileEditor).GetMethod("CriteriaChanged", PrivateInstance)!.Invoke(editor, null);
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { matchedAssets = 99 }) });
        await preview;
        Assert.IsNull(GetField(editor, "_matchCount"));
        Assert.IsFalse((bool)GetField(editor, "_matchCountLoading")!);
    }

    [TestMethod]
    public async Task FailedWeatherSearch_ShowsFeedbackAndAllowsRetry()
    {
        using var client = new HttpClient(new FailureHandler("network")) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(client);
        var editor = new ProfileEditor();
        typeof(ProfileEditor).GetProperty("HttpFactory", PrivateInstance)!.SetValue(editor, factory);
        typeof(ProfileEditor).GetField("_locationQuery", PrivateInstance)!.SetValue(editor, "Test city");
        await (Task)typeof(ProfileEditor).GetMethod("SearchLocationAsync", PrivateInstance)!.Invoke(editor, null)!;
        Assert.IsFalse((bool)GetField(editor, "_locationSearching")!);
        Assert.IsNotNull(GetField(editor, "_locationError"));
    }

    [TestMethod]
    public async Task ClearingNewLocation_RestoresUnchangedEditorState()
    {
        using var client = _factory.CreateClient();
        var (editor, _) = await LoadEditorAsync(client, new Profile { Id = "location-undo", Name = "Undo" });
        var location = (LocationFilter)GetField(editor, "_locationFilter")!;
        location.City = "Test city";
        Assert.IsTrue((bool)typeof(ProfileEditor).GetProperty("IsDirty", PrivateInstance)!.GetValue(editor)!);
        location.City = "";
        var picker = new ImmichLoungeCompanion.Components.Shared.LocationFilterPicker();
        typeof(ImmichLoungeCompanion.Components.Shared.LocationFilterPicker).GetProperty("Value")!.SetValue(picker, location);
        await (Task)typeof(ImmichLoungeCompanion.Components.Shared.LocationFilterPicker).GetMethod("NotifyChangedAsync", PrivateInstance)!.Invoke(picker, null)!;
        Assert.IsFalse((bool)typeof(ProfileEditor).GetProperty("IsDirty", PrivateInstance)!.GetValue(editor)!);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SuccessfulSave_PreservesNewerEditsAndOnlyClosesWhenUnchanged(bool editDuringSave)
    {
        using var client = _factory.CreateClient();
        var (editor, profile) = await LoadEditorAsync(client, new Profile { Id = "slow-save", Name = "Save" });
        using var handler = new PendingPreviewHandler();
        using var pendingClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(pendingClient);
        var navigation = new EditorTestNavigation();
        typeof(ProfileEditor).GetProperty("HttpFactory", PrivateInstance)!.SetValue(editor, factory);
        typeof(ProfileEditor).GetProperty("Nav", PrivateInstance)!.SetValue(editor, navigation);
        profile.Slideshow.IntervalSeconds = 12;
        var saving = (Task)typeof(ProfileEditor).GetMethod("SaveAsync", PrivateInstance)!.Invoke(editor, null)!;
        await handler.Started.Task;
        if (editDuringSave) { profile.Slideshow.IntervalSeconds = 31; }
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await saving;
        Assert.AreEqual(editDuringSave ? 31 : 12, ((Profile)GetField(editor, "_profile")!).Slideshow.IntervalSeconds);
        Assert.AreEqual(editDuringSave, (bool)typeof(ProfileEditor).GetProperty("IsDirty", PrivateInstance)!.GetValue(editor)!);
        Assert.AreEqual(!editDuringSave, navigation.Navigated);
        Assert.IsFalse((bool)GetField(editor, "_saving")!);
        if (editDuringSave)
        {
            Assert.AreSame(profile, GetField(editor, "_profile"));
            Assert.IsNotNull(GetField(editor, "_saveMessage"));
            factory.CreateClient("local").Returns(client);
            await (Task)typeof(ProfileEditor).GetMethod("SaveAsync", PrivateInstance)!.Invoke(editor, null)!;
            Assert.IsTrue(navigation.Navigated);
            Assert.IsNull(GetField(editor, "_saveMessage"));
            Assert.AreEqual(31, (await client.GetFromJsonAsync<Profile>("/api/profiles/slow-save"))!.Slideshow.IntervalSeconds);
        }
    }

    [TestMethod]
    public async Task PreparedPayload_DoesNotShareEditableLocation()
    {
        using var client = _factory.CreateClient();
        var (editor, _) = await LoadEditorAsync(client, new Profile { Id = "location-copy", Name = "Copy" });
        var location = (LocationFilter)GetField(editor, "_locationFilter")!;
        location.City = "First city";
        var payload = BuildPayload(editor);
        location.City = "Newer city";
        Assert.AreEqual("First city", payload.LocationFilter!.City);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MatchSelector_IsShownOnlyForEditableRules(bool advanced)
    {
        using var client = _factory.CreateClient();
        var profile = new Profile { Id = "render-rules", Name = "Rules" };
        if (advanced)
        {
            profile.AssetFilter = new() { Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.Or,
                Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Album, Id = "a" },
                    new() { Kind = AssetFilterRuleKind.Group, Operator = AssetFilterGroupOperator.And,
                        Children = [new() { Kind = AssetFilterRuleKind.Condition, Type = AssetFilterConditionType.Person, Id = "p" }] }] };
        }
        Assert.AreEqual(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(client);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory);
        services.AddSingleton<NavigationManager>(new EditorTestNavigation());
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(Substitute.For<ImmichLoungeCompanion.Services.IDisplayDateFormattingService>());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ProfileEditor>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = profile.Id }));
            return output.ToHtmlString();
        });
        Assert.AreEqual(!advanced, html.Contains("Match selected sources:"));
        if (advanced) { Assert.IsTrue(html.Contains("advanced content rules")); }
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public async Task PeoplePresenceCheckboxGatesNewSelectionsAndAllowsDisablingExistingFilter(bool supported, bool enabled)
    {
        _factory.ImmichClient.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>())
            .Returns(new SearchApiCapabilities(supported ? "3.2.0" : "3.1.0", supported));
        using var client = _factory.CreateClient();
        var profile = new Profile { Id = "render-people", Name = "People", OnlyPhotosWithPeople = enabled };
        Assert.AreEqual(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(client);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory);
        services.AddSingleton<NavigationManager>(new EditorTestNavigation());
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(Substitute.For<ImmichLoungeCompanion.Services.IDisplayDateFormattingService>());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ProfileEditor>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = profile.Id }));
            return output.ToHtmlString();
        });
        var input = System.Text.RegularExpressions.Regex.Match(html, "<input[^>]*id=\"only-photos-with-people\"[^>]*>");
        Assert.IsTrue(input.Success);
        Assert.AreEqual(!supported && !enabled, input.Value.Contains("disabled"));
        Assert.AreEqual(enabled, input.Value.Contains("checked"));
        Assert.AreEqual(!supported, html.Contains("Requires Immich 3.2 or later"));
    }

    private sealed class EditorTestNavigation : NavigationManager
    {
        public bool Navigated { get; private set; }
        public EditorTestNavigation() { Initialize("http://test/", "http://test/profiles/slow-save/edit"); }
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Navigated = true;
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class FailureHandler(string failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => failure switch
            {
                "network" => throw new HttpRequestException("Offline"),
                "timeout" => throw new TaskCanceledException("Timeout"),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Gateway failed</html>") })
            };
    }

    private sealed class PendingPreviewHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            return Response.Task;
        }
    }

    private static async Task<(ProfileEditor Editor, Profile Profile)> LoadEditorAsync(HttpClient client, Profile profile)
    {
        var created = await client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        return await ReloadEditorAsync(client, profile.Id);
    }

    private static async Task<(ProfileEditor Editor, Profile Profile)> ReloadEditorAsync(HttpClient client, string id)
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("local").Returns(client);
        var editor = new ProfileEditor();
        typeof(ProfileEditor).GetProperty("Id")!.SetValue(editor, id);
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
