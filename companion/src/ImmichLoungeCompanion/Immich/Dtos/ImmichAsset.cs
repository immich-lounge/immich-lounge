using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImmichLoungeCompanion.Immich.Dtos;

// Shape returned by POST /api/search/assets and embedded in GET /api/memories
public class ImmichAsset
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";          // "IMAGE" | "VIDEO"
    public string? LivePhotoVideoId { get; set; }
    [JsonPropertyName("exifInfo")]
    public ImmichExifInfo? ExifInfo { get; set; }
    public List<ImmichPerson> People { get; set; } = [];
}

public class ImmichExifInfo
{
    public string? DateTimeOriginal { get; set; }   // ISO 8601
    [JsonPropertyName("exifImageWidth")]
    public int? ExifImageWidth { get; set; }
    [JsonPropertyName("exifImageHeight")]
    public int? ExifImageHeight { get; set; }
    [JsonPropertyName("fileSizeInByte")]
    public long? FileSizeInByte { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}
