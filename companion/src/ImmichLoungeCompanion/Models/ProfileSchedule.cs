using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImmichLoungeCompanion.Models;

public class ProfileSchedule
{
    public string Id { get; set; } = "";
    [JsonRequired] public string Name { get; set; } = "";
    [JsonRequired] public string FallbackProfileId { get; set; } = "";
    [JsonRequired] public bool Enabled { get; set; } = true;
    [JsonRequired] public string TimeZoneId { get; set; } = "UTC";
    [JsonRequired] public List<ProfileScheduleEntry> Entries { get; set; } = [];
}

public class ProfileScheduleEntry
{
    public string ProfileId { get; set; } = "";
    public List<ScheduleDay> Days { get; set; } = [];
    public string Start { get; set; } = "08:00";
    public string End { get; set; } = "18:00";
}

[JsonConverter(typeof(JsonStringEnumConverter<ScheduleDay>))]
public enum ScheduleDay
{
    Sunday,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday
}
