namespace ImmichLoungeCompanion.Models;

public record PlaylistEntry(
    string Id,
    string Type,                      // "photo" | "video" | "livePhoto"
    string? SourceLabel,
    string? LivePhotoVideoId,
    string? BirthdayCaption = null,
    bool IsMemory = false
);
