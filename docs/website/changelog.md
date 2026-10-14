---
description: "Read user-facing release notes and changes to Immich Lounge."
icon: material/history
---

# Changelog

User-facing release notes for Immich Lounge live here.

For technical release details, see [CHANGELOG.md](https://github.com/immich-lounge/immich-lounge/blob/master/CHANGELOG.md).

## Unreleased

- Retry unreadable original photos using JPEG previews.

## 1.2.1 — 2026-10-14

- Smoother transitions into side-by-side portrait photos: the channel and screensaver load the next pair while the current photo is showing.
- Show the installed companion version in the navigation.
- Clearer browser icons for the companion and website.
- Update the website documentation builder.
- Refresh screenshots with the current interface and non-personal demo photos.

## 1.2.0 — 2026-10-11

- Companion and Roku apps version 1.2.0.
- Keep regular photos available when optional memories are temporarily unavailable.
- Include memories saved in Immich throughout the year, with Immich 3.2 or later.
- Show all available people in profile pickers, including libraries with more than 500 people.

- With Shuffle off, choose Newest first or Oldest first to play photos by their original date. Existing profiles keep Newest first.
- Choose **Only photos with people** to show photos with any detected face, including unnamed people, shared albums, and memories. Requires Immich 3.2 or later.

- Use shared Immich albums in slideshow profiles, including filters that match or exclude people. For another user's people and names, share the people separately in Immich 3.3 or later. New editor hints explain how to select the album and person and use **Every selection** to show only matching photos.

- Choose All photos, Selected sources, or Memories only. Optionally show matching memories first without duplicates. Memories refresh and shuffled photos get a fresh order each day; the Roku waits for the current photo to finish and uses your latest saved profile. Existing profiles keep their selection until changed.

- Fix a Roku timing issue that could delay a fresh shuffle after the last photo.

- Add checksums and signed build provenance to Immich Lounge candidate downloads so maintainers can verify their source and integrity.

- Add ARM64 companion images for 64-bit ARM hosts. Docker automatically selects the image for your host.

- Choose how photo locations appear: city and country, full city/state/province/country, city only, state/province and country, or country only. Existing profiles keep city and country.

## 1.1.0 — 2026-10-11

- Make the website easier to read with a true light mode, clearer links and buttons, and simpler card backgrounds. Follow your system theme by default and remember your choice when you switch modes.

- Companion and Roku apps version 1.1.0.
- A simpler companion editor groups Photos, Playback, and Appearance settings, with sample photos when previewing your selection.

- Clarify that Immich 3.1 or later is supported. Birthday memories require Immich 3.3 or later; older versions may work but are not actively tested.

- With **Shuffle** enabled, get a fresh photo order after each complete loop in both Roku apps. Each device shuffles independently.
- Fixed late playlist refreshes moving playback back to an earlier group of photos.
- Choose **Restart with fresh shuffle** from the channel settings menu to start a new order immediately. Reopening an app still resumes where it left off.
- Keep cached photos looping when the companion is offline, and let shuffle restarts complete during photo-load recovery.

- Set the speed of fade, slide, and zoom transitions. Existing profiles keep the same half-second transitions.

- Choose whether photos must match any or all selected sources, and exclude photos containing selected people, including memories.

- Start cached channel and screensaver photos in about five seconds plus image-loading time when the companion is unavailable.
- Add birthday memories from Immich 3.3 or later, with optional captions. Choose birthdays, On This Day memories, or both.
- Choose a time zone for memories and refresh them when the day changes. Profiles with only memories selected now show only those memories.

- Added reusable schedules that switch between profiles at set times and days, with a fallback profile. Choose a fixed profile or a schedule on each Roku.


- Filter profiles by country, state / province, or city, including memories.
- Get location suggestions from your Immich library.
- Automatically use the search API supported by your Immich server. Existing profiles need no migration.
- See the detected server version when testing your connection.

- Fixed the developer test command accepting results from an earlier Roku test run.

- Added an option to show consecutive portrait photos side by side without cropping.

- Added **Zoom to fit** to fill the screen with landscape photos by cropping their edges.

- Fixed photo loading when the Immich server URL ends with a slash.

## 2026-04-12

- Roku channel and screensaver version 1.0.0008.
- The Roku channel now stays open when a profile has no photos and shows a clearer message with the companion URL and profile-change hint.
- Weather, clock, and display setting changes now apply on Roku automatically after a short refresh, without reshuffling the current slideshow playlist.
- Playlist refreshes and resume behavior are more stable when playlists are refreshed or fetched in batches.
- Shared Immich albums are documented as not supported yet because Immich search does not reliably return assets from shared albums.

## 2026-03-30

- Initial Release
