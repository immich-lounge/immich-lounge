---
description: "Read user-facing release notes and changes to Immich Lounge."
icon: material/history
---

# Changelog

User-facing release notes for Immich Lounge live here.

For technical release details, see [CHANGELOG.md](https://github.com/immich-lounge/immich-lounge/blob/master/CHANGELOG.md).

## Unreleased

- Choose how photo locations appear: city and country, full city/state/province/country, city only, state/province and country, or country only. Existing profiles keep city and country.

- Make the website easier to read with a true light mode, clearer links and buttons, and simpler card backgrounds. Follow your system theme by default and remember your choice when you switch modes.

## 1.1.0 — 2026-10-11

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
