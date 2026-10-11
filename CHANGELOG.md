# Changelog

Technical release notes for Immich Lounge live here.

For a shorter user-facing summary, see [docs/website/changelog.md](./docs/website/changelog.md).

## Unreleased

- Reshuffle at each Roku playlist loop using a device-selected seed carried in the playlist version. Keep windows and resume deterministic within a cycle without mutating the shared companion cache, including playlists shorter than 50 entries.
- Reject playlist refreshes whose requested window or shuffle cycle was superseded during playback; retry at the current position and stop obsolete batch fetches when applying a refresh.
- Add a channel settings action to restart with a fresh shuffle, canceling obsolete requests and retaining current playback and resume state if the companion request fails.
- Back off failed Roku playlist-window requests for 60 seconds while replaying cached photos. Apply deferred shuffle restarts from transition, load-recovery, and resume callbacks, and cancel obsolete asset-retry timers.

- Add `slideshow.transitionDurationSeconds` (0.1–5, below the photo interval), defaulting to 0.5 for existing profiles. Snapshot the duration per transition so Roku animations, background fades, and completion timers agree in both modes.

- Expose flat AND/OR source matching in the companion editor and add people exclusions applied to sources and memories. Preserve nested API rules and use separate person queries for legacy and structured search compatibility.

- Bound Roku cached startup to a two-second profile wait and three-second playlist wait, using elapsed time even while other events arrive. Stop abandoned startup tasks while retaining selection/effective-profile cache checks and cold-start wait limits.
- Add opt-in birthday memories for Immich 3.3+, independent of On This Day memories, with optional birthday captions in both Roku overlay styles. Preserve album labels and omit date-sensitive captions from offline registry playlists.
- Add a per-profile memories time zone and rebuild memory playlists when its date changes. Memory-only profiles now select only the enabled memories instead of the entire library.
- Hydrate missing memory EXIF only when location or minimum-file-size filters need it; reuse metadata already fetched for regular sources and skip photos deleted since memory generation.

- Added independent named schedules under `/data/schedules`, with explicit fallback profiles, time zones and weekly slots. Roku can choose a fixed profile or a schedule independently in each mode; playback switches settings and playlist together and tracks effective profile identity for resume/cache requests.


- Detect the Immich server version and select legacy metadata search or the structured Search API v2 introduced in 3.2; cache detection briefly and refresh it on Test Connection.
- Preserve legacy ID-list matching, inclusive date boundaries, and profile storage; support both numbered and cursor pagination without mutating caller requests.
- Add optional country/state/city profile filters, exact-name suggestions, and location filtering of added memories.
- Keep version-detection failure compatible with legacy search; surface search failures without retrying broader queries.

- Bind Roku test completion to a unique ID in each test deployment, ignoring buffered results from earlier runs. Added local runner regression checks to CI.

- Added optional `display.sideBySidePortraits` in both Roku modes, using loaded bitmap dimensions to pair adjacent portrait photos without adding playlist or registry fields. Added pair-aware navigation, refresh handling, and secondary-image fallback.

- Added optional `display.zoomToFit` for landscape photos in both Roku modes; existing profiles default to showing the whole photo. Cover layouts round up to the 3-pixel grid.

- Normalize Immich base URLs in Roku media, background, and metadata requests, including cached profiles with trailing slashes (issue #7, based on PR #14).

## 2026-04-12

- Bumped Roku channel and screensaver manifests to version 1.0.0008.
- Kept the Roku channel open when the selected profile has no photos, with a clearer empty-state screen and profile-change hint.
- Added a Roku metadata-only profile refresh that periodically applies display, clock, and weather configuration changes without replacing the current playlist.
- Added playlist window versioning so Roku refreshes and batched playlist fetches can avoid reshuffling or jumping unexpectedly when the companion cache is still current.
- Improved Roku playlist resume state by tracking playlist version and preserving the currently displayed asset when possible after refresh.
- Documented that shared Immich albums are not currently supported because Immich search does not reliably return assets from shared albums.
- Kept shared-album retrieval helpers in the companion codebase, but disabled shared album listing until upstream Immich search support is available.

## 2026-03-30

- Initial Release
