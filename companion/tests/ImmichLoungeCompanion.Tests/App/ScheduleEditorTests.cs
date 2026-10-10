using System.Reflection;
using ImmichLoungeCompanion.Components.Pages;
using ImmichLoungeCompanion.Models;
using Microsoft.AspNetCore.Components;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class ScheduleEditorTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void NewScheduleIdFollowsNameUntilCustomized()
    {
        var schedule = new ProfileSchedule();
        var editor = CreateEditor(schedule);
        SetName(editor, "Living Room");
        Assert.AreEqual("living-room", schedule.Id);
        SetName(editor, "Weekend Photos");
        Assert.AreEqual("weekend-photos", schedule.Id);
        typeof(ScheduleEditor).GetMethod("SetScheduleId", PrivateInstance)!.Invoke(editor, [new ChangeEventArgs { Value = "custom-schedule" }]);
        SetName(editor, "New Name");
        Assert.AreEqual("custom-schedule", schedule.Id);
        Assert.AreEqual("New Name", schedule.Name);
    }

    [TestMethod]
    public void RenamingExistingSchedulePreservesItsId()
    {
        var schedule = new ProfileSchedule { Id = "existing-schedule", Name = "Original" };
        var editor = CreateEditor(schedule);
        typeof(ScheduleEditor).GetProperty("Id")!.SetValue(editor, schedule.Id);
        SetName(editor, "New Name");
        Assert.AreEqual("existing-schedule", schedule.Id);
        Assert.AreEqual("New Name", schedule.Name);
    }

    private static ScheduleEditor CreateEditor(ProfileSchedule schedule)
    {
        var editor = new ScheduleEditor();
        typeof(ScheduleEditor).GetField("_schedule", PrivateInstance)!.SetValue(editor, schedule);
        return editor;
    }

    private static void SetName(ScheduleEditor editor, string name) =>
        typeof(ScheduleEditor).GetMethod("SetScheduleName", PrivateInstance)!.Invoke(editor, [name]);
}
