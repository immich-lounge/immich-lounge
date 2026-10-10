---
description: "Manage the Immich connection, slideshow profiles, playlists, and schedules in the companion web app."
icon: material/monitor-dashboard
---

# Using the Companion

This page covers the day-to-day companion web app after the initial connection to immich is already set up.

For install and first-run setup, see [Getting Started](./getting-started.md) and [Installation](./installation.md).

## Connection page

The Connection page stores the shared immich connection used by every profile.

Use it when you need to:

- update the immich server URL
- replace the API key
- save the entered settings and verify access with **Save & Test Connection**

**Save** stores the settings without testing. **Save & Test Connection** stores them first, then checks access to Immich. A failed test does not undo the saved settings.

If you need to generate or rotate the key, see [Immich API Key](./api-key.md).

The page includes key creation instructions and an expandable permission list. After a successful connection test, follow **Next: create or choose a profile**.

![Immich Lounge companion connection page](./assets/screenshots/companion-connection.png){ .doc-screenshot .on-glb }
<p class="doc-caption">The connection page is the one shared integration point between the companion and immich.</p>

## Profiles page

The Profiles page is the main hub for ongoing changes.

Use it when you want to:

- create a new profile for a room, use case, or screensaver
- edit an existing profile
- remove a profile you no longer need
- compare which profiles exist before switching the Roku to a different one

When there are no profiles yet, choose **Create a Profile**. Enter a name; the companion generates a profile ID automatically. Use **Customize profile ID** if you want a different ID or the generated one is already in use.

**Refresh Playlist** requests a rebuild for one profile; **Refresh All Playlists** requests it for every profile. The confirmation means the request was accepted, not that the background rebuild has finished.

Deleting a profile requires confirmation. It removes the companion profile, not your Immich photos. Update any schedule or Roku selection that refers to the deleted profile.

![Immich Lounge companion profiles page](./assets/screenshots/companion-profiles.png){ .doc-screenshot .on-glb }
<p class="doc-caption">Profiles are where most day-to-day tuning happens after setup is complete.</p>

## Profile editor

Each profile controls both what the Roku shows and how playback should feel.

The summary at the top shows the selected sources, photo interval, shuffle setting, and image quality. **Content Sources** and **Slideshow** stay open; expand **Location**, **Media**, **Overlay & Clock**, **Date & Time**, or **Weather** when needed. These sections keep their existing settings when collapsed.

You can edit display settings before connecting Immich. Follow **Configure connection** to enable source selection and match previews. If sources cannot load, check the connection and use the picker’s retry button; the rest of the editor remains usable.

| Area | What you manage there |
|---|---|
| Content | Albums, people, tags, memories, and optional date filters |
| Playback | Interval, shuffle, transitions, photo motion, and refresh timing |
| Display | Overlay style, fields, background effects, date formatting, weather, and image quality |

![Immich Lounge companion profile editor](./assets/screenshots/companion-profile-editor.png){ .doc-screenshot .on-glb }
<p class="doc-caption">The profile editor combines content selection, playback settings, and visual tuning in one place.</p>

## Schedules page

Schedules choose which profile plays at different times; profiles still hold the photos and display settings.

1. Create the profiles you want to use.
2. Open **Schedules** and choose **New Schedule**.
3. Enter a name, choose the time zone and fallback profile, and add weekly time slots.
4. Save the schedule, then select it on Roku instead of a fixed profile.

The schedule ID is generated from the name. Expand **Customize schedule ID** to change it before saving, including when the generated ID is already in use. Existing schedule IDs stay fixed.

Outside the time slots, the fallback profile plays. Overlapping slots use the first matching row; move rows up to change priority. The channel and screensaver can follow different schedules.

Deleting a schedule keeps its profiles. Choose another schedule or profile on any Roku that used the deleted schedule.

For time-zone, fallback, and offline behavior, see [Schedules](./configuration.md#schedules).

## Typical companion tasks

- make one profile for the main channel and another for the screensaver
- keep a simpler profile for weaker Roku hardware or slower networks
- duplicate a working setup by recreating a similar profile with different albums or people
- update the immich API key in one place when it changes

## What the companion does not do

- it does not proxy image delivery
- it does not store the API key inside the profile JSON files on disk
- it does not control Roku-side cache clearing or profile switching directly

For Roku playback behavior, remote controls, and the on-device settings menu, see [Using the Roku Apps](./roku-apps.md).
