using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImmichLoungeCompanion.Models;

public class Profile
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<ContentSource> ContentSources { get; set; } = [];
    public AssetFilterRule? AssetFilter { get; set; }
    public MediaTypes MediaTypes { get; set; } = new();
    public DateFilter? DateFilter { get; set; }
    public SlideshowSettings Slideshow { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public QualitySettings Quality { get; set; } = new();
    public string ImageQuality { get; set; } = "preview";
    public WeatherSettings? Weather { get; set; }
}

public class ContentSource
{
    public string Type { get; set; } = "";   // "album" | "person" | "tag" | "memories"
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<AssetFilterRuleKind>))]
public enum AssetFilterRuleKind
{
    Group,
    Condition
}

[JsonConverter(typeof(JsonStringEnumConverter<AssetFilterGroupOperator>))]
public enum AssetFilterGroupOperator
{
    And,
    Or
}

[JsonConverter(typeof(JsonStringEnumConverter<AssetFilterConditionType>))]
public enum AssetFilterConditionType
{
    Album,
    Person,
    Tag
}

[JsonConverter(typeof(JsonStringEnumConverter<DisplayFormatSource>))]
public enum DisplayFormatSource
{
    Roku,
    Profile
}

public class AssetFilterRule
{
    public AssetFilterRuleKind Kind { get; set; }
    public AssetFilterGroupOperator? Operator { get; set; }
    public List<AssetFilterRule> Children { get; set; } = [];
    public AssetFilterConditionType? Type { get; set; }
    public string? Id { get; set; }
    public string? Label { get; set; }
}

public class MediaTypes
{
    public bool Photos { get; set; } = true;
    public bool Videos { get; set; } = false;
    public bool VideoAudio { get; set; } = false;
    public bool LivePhotos { get; set; } = false;
}

public class DateFilter
{
    public string Type { get; set; } = "";    // "range" | "rolling"
    // Range mode
    public string? From { get; set; }
    public string? To { get; set; }
    // Rolling mode
    public int? Amount { get; set; }
    public string? Unit { get; set; }         // "days" | "weeks" | "months" | "years"
}

public class SlideshowSettings
{
    public int IntervalSeconds { get; set; } = 10;
    public bool Shuffle { get; set; } = true;
    public string TransitionEffect { get; set; } = "fade";
    public string PhotoMotion { get; set; } = "none";
    public int RefreshIntervalMinutes { get; set; } = 60;
    public bool PreventScreensaver { get; set; } = false;
}

public class DisplaySettings
{
    public DisplayFormatSource FormatSource { get; set; } = DisplayFormatSource.Roku;
    public string OverlayStyle { get; set; } = "bottom";
    public string BackgroundEffect { get; set; } = "blur";
    public List<string> OverlayFields { get; set; } = ["date", "location", "album", "people"];
    public string OverlayBehavior { get; set; } = "fade";
    public int OverlayFadeSeconds { get; set; } = 5;
    public bool ClockAlwaysVisible { get; set; } = true;
    public string ClockFormat { get; set; } = "HH:mm";
    public string WeatherUnit { get; set; } = "celsius";
    public bool ShowTimer { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public string DateFormat { get; set; } = "d MMMM yyyy";
    public string Locale { get; set; } = "en-US";
}

public class QualitySettings
{
    public int? MinFileSizeKb { get; set; }
}

public class WeatherSettings
{
    public bool Enabled { get; set; } = false;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int PollIntervalMinutes { get; set; } = 20;
}
