# Changelog

Technical release notes for Immich Lounge live here.

For a shorter user-facing summary, see [docs/website/changelog.md](./docs/website/changelog.md).

## Unreleased

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
