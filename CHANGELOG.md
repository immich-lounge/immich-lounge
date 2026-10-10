# Changelog

Technical release notes for Immich Lounge live here.

For a shorter user-facing summary, see [docs/website/changelog.md](./docs/website/changelog.md).

## Unreleased

- Detect the Immich server version and select legacy metadata search or the structured Search API v2 introduced in 3.2; cache detection briefly and refresh it on Test Connection.
- Preserve legacy ID-list matching, inclusive date boundaries, and profile storage; support both numbered and cursor pagination without mutating caller requests.
- Add optional country/state/city profile filters, exact-name suggestions, and location filtering of added memories.
- Keep version-detection failure compatible with legacy search; surface search failures without retrying broader queries.

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
