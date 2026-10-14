# Changelog

Technical release notes for Immich Lounge live here.

For a shorter user-facing summary, see [docs/website/changelog.md](./docs/website/changelog.md).

## Unreleased

- Retry failed original-quality Roku photos once with the normalized JPEG preview URL before skipping the asset.
- Add validated date range and rolling-window editor controls, preserve timestamp profiles, and calculate rolling bounds with the injected clock.
- Expand date-only upper bounds through the final UTC day in both search formats; preserve explicit timestamps and omit empty bounds.

## 1.2.1 — 2026-10-14

- Set companion assembly/package version to `1.2.1` and both Roku manifests to `1.2.0001`.
- Preload the next portrait partner during the current slide in both Roku modes, retain its image through foreground loading, and validate the partner URL before reuse. Preserve pairing, navigation, and secondary-image fallback behavior.
- Show the companion's assembly version in navigation and fix its Razor rendering.
- Simplify browser favicons for small sizes and provide SVG favicons for the companion and public website.
- Update the public documentation builder to Zensical `0.0.70`.
- Refresh fullscreen playback and companion editor screenshots from a bundled demo album; add a portrait-pair screenshot to configuration documentation.

## 1.2.0 — 2026-10-11

- Set companion assembly/package version to `1.2.0` and both Roku manifests to `1.2.0000`.
- Allow playlists with regular sources to rebuild when optional memory retrieval times out or returns a transient network/server error. Log a warning; retain errors for memory-only profiles, permission failures, exclusions, and caller cancellation.
- Add opt-in saved memories for Immich 3.2+, with complete pagination and deduplication. Include past saved memories, exclude upcoming memories, and retain birthday captions only for active daily birthday memories.
- Fetch every Immich people page for the profile pickers, deduplicate shared people, and detect pagination that makes no progress.

- Add `slideshow.dateOrder` (`newestFirst` or `oldestFirst`) for unshuffled playlists. Expose Photo order when Shuffle is off and retain newest-first ordering for existing profiles.
- Add opt-in `onlyPhotosWithPeople` profile filtering using Immich 3.2+ `hasPeople`, including shared album scopes and memories. Gate the editor by detected capability, preserve existing profiles, and reject unsupported filtering instead of dropping the predicate.

- Support shared Immich albums as playlist sources and preserve selected album scope when matching or excluding people, including nested filter rules. Retain owned/partner searches, deduplicate matches, and propagate scoped search failures. Add legacy/cursor pagination boundary regressions and shared-people setup guidance in the editor and docs.

- Add explicit photo sources (`all`, `selected`, `memories`) and optional memory priority. Preserve omitted-source profile behavior; constrain memories to explicit selections and filters, retain priority through paged shuffle cycles, and renew shuffle/version on the profile's next memory day. Roku polls daily-memory profiles each minute and applies the new day at a slide boundary, preserving same-day order and offline fallback. Discard queued daily results on later profile edits; fetch the playlist head for unknown-day live resumes while retaining cached offline windows.

- Keep a Roku playlist fetch reserved until its result callback consumes it. Prevent the shuffle boundary from replacing a queued task before its RUN state becomes visible, which could read an empty result and delay reshuffling until the retry.

- Add signed GitHub build provenance and downloadable inner-ZIP SHA256 checksums to candidate companion, Roku channel/screensaver, and docs artifacts; enforce aligned source versions, required files, and strict docs validation.

- Publish companion images for Linux AMD64 and ARM64 using native SDK cross-compilation. Build and smoke-test both architectures in CI, including the health endpoint and writable data volume.

- Add `display.locationFormat` with five location label formats, defaulting to city/country. Share formatting across Roku overlay styles and modes; trim missing/blank values and deduplicate repeated place names. Apply profile format changes to the current metadata label.

## 1.1.0 — 2026-10-11

- Give the Zensical website distinct light and dark palettes, follow the system color preference until the visitor chooses a theme, replace dark card gradients with theme-aware solid surfaces, and improve link, button, and keyboard-focus contrast.

- Set companion assembly/package version to `1.1.0` and both Roku manifests to `1.1.0000`.
- Reorganize the companion editor into Photos, Playback, and Appearance, add bounded thumbnail samples to selection previews, and preserve pending-save edits and advanced matching rules.

- Document Immich 3.1 as the minimum supported version, with Immich 3.3 required for birthday memories. Retain legacy API compatibility; older versions may work but are not actively tested.

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
