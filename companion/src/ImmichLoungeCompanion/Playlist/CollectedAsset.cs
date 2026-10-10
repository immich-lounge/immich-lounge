using ImmichLoungeCompanion.Immich.Dtos;

namespace ImmichLoungeCompanion.Playlist;

public record CollectedAsset(ImmichAsset Asset, string? SourceLabel, string? BirthdayCaption = null);
