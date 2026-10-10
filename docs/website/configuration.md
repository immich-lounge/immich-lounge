---
description: "Choose photo sources, playback settings, overlays, weather, and schedules for your Roku slideshow."
icon: material/tune-variant
---

# Configuration

<div class="section-intro">
Most users only need three things here: connect the companion to immich, create one or more profiles, and tune how the slideshow should look on Roku.
</div>

For the companion pages you use after setup, see [Using the Companion](./companion-app.md).

For channel controls, profile switching on the Roku, cache clearing, and screensaver behavior after setup, see [Using the Roku Apps](./roku-apps.md).

## Connection settings

These settings are shared by every profile:

- immich server URL
- immich API key

Use an immich URL that the Roku can reach on your LAN. `localhost`, `127.0.0.1`, `::1`, and wildcard bind addresses are not valid here.

Use **Save & Test Connection** to save the settings and check that the companion can reach Immich. Settings stay saved even if the test fails. The result also shows the server version and selected search API. Immich Lounge selects Search API v2 for stable Immich 3.2+ servers and legacy search for older servers. If version detection is unavailable, it uses legacy search with the same filters.

Detection refreshes automatically after five minutes; **Save & Test Connection** refreshes it immediately. No settings or profile migration is needed.

![Immich Lounge companion connection page](./assets/screenshots/companion-connection.png){ .doc-screenshot .on-glb }
<p class="doc-caption">The connection page stores the single immich connection used by all profiles.</p>

> **Security note:** The companion is for trusted home networks. It does not provide public-internet authentication.

## Profiles

A profile decides what to show and how to show it. You can create as many as you want.

After changing a profile's content sources, **Preview Match Count** uses the new selection. Save the profile to apply it to the playlist when the Roku next refreshes it.

Saving a profile or connection settings clears the affected companion playlists. Any build already using the previous settings is discarded; the next playlist request builds from the saved settings.

Choose **Any (OR)** to include photos matching any selected album, person, or tag, or **All (AND)** to require every selection. No selected sources means the whole library. **Exclude people (NOT)** removes photos containing any excluded person from all results, including added memories. For example, select your children with **Any**, then exclude yourself to show either child without you.

Memories are added to the source results; they do not have to match the selected sources. Location restrictions and people exclusions still apply to memories.

Profiles with nested content rules created through the API keep those rules when edited in the companion. Their source selectors are unavailable; edit the nested rules through the API. Excluded people, memories, and display settings can still be changed in the companion.

The companion collects all matching result pages and removes duplicates. With **Shuffle** enabled, it randomizes the playlist when building it; it does not randomly choose a smaller sample. The Roku fetches the playlist in windows and keeps its place while the playlist version stays the same. Rebuilding can change the order. With shuffle off, photos are ordered by their original date, newest first.

![Immich Lounge companion profiles page](./assets/screenshots/companion-profiles.png){ .doc-screenshot .on-glb }
<p class="doc-caption">Each profile can target a different room, use case, or screensaver setup.</p>

<div class="feature-grid">
  <div class="card">
    <h3>Content sources</h3>
    <p>Build a profile from albums, people, tags, and memories. Sources are combined into one playlist.</p>
  </div>
  <div class="card">
    <h3>Date filters</h3>
    <p>Limit a profile to a fixed range or a rolling window like the last 6 months.</p>
  </div>
  <div class="card">
    <h3>Slideshow controls</h3>
    <p>Change interval, shuffle, transitions, photo motion, and playlist refresh timing.</p>
  </div>
  <div class="card">
    <h3>Display settings</h3>
    <p>Choose overlays, clock and weather, date formatting, and background effects.</p>
  </div>
</div>

## What you can configure

| Area | What it covers |
|---|---|
| Content | Albums, people, tags, memories, and optional date and location filtering |
| Playback | Interval, shuffle, transition effect, photo motion, and playlist refresh interval |
| Display | Overlay style and fields, background effect, date and time formatting, weather, and image quality |

> **Shared albums:** Immich Lounge currently lists only albums owned by the configured Immich user. Shared albums are hidden because Immich search does not yet reliably return assets from shared albums. Track upstream support in the Immich discussion [Shared Albums in Search Results](https://github.com/immich-app/immich/discussions/2995).

![Immich Lounge slideshow with overlay, clock, and weather](./assets/screenshots/slideshow.png){ .doc-screenshot .on-glb }
<p class="doc-caption">Example slideshow with a left-side overlay, persistent clock, and weather.</p>

When weather is enabled for a profile, you can choose whether the Roku shows temperatures in Celsius or Fahrenheit.

### Zoom to fit

In the profile's **Slideshow** settings, enable **Zoom to fit (landscape photos)** to fill the screen with landscape photos. This preserves the photo's proportions and crops the edges as needed. Portrait and square photos still show the whole image.

The setting is off by default and works in both the channel and screensaver. After the Roku refreshes the profile, it applies to the next photo displayed.

### Transition duration

Set **Transition duration (seconds)** to 0.1–5 seconds, shorter than the photo interval. It controls fade, slide, and zoom transitions in both the channel and screensaver, including portrait-pair fades and background crossfades. **None** changes photos immediately. Existing profiles keep the 0.5-second default; changes apply on the next transition after the Roku refreshes its profile.

### Side-by-side portraits

Enable **Show portrait photos side by side** in the profile's **Slideshow** settings to pair consecutive portrait photos. Both photos are shown in full for one slideshow interval. Landscape, square, and unmatched portrait photos show alone. Pairs stay within the current playlist window and do not cross its boundary.

The setting is off by default and works in both the channel and screensaver, including cached playlists. The overlay describes the left photo. Pairs fade together (or switch immediately if transitions are set to None); Ken Burns motion still applies to single photos.

If the right photo cannot load, the left shows alone and the right is retried as the next item. Changes apply to the next photo displayed after the profile refresh.

## Location filters

In the profile editor, fill in **Country**, **State / province**, or **City** to limit the playlist by location. Suggestions come from locations in your Immich library. Use the exact names stored in Immich; if suggestions are unavailable, you can enter those names manually.

All filled fields must match. Blank fields impose no restriction. For example, a country alone includes all matching locations in that country. A country and city together require both to match. The location filter applies to selected sources and added memories; photos without the required location metadata are excluded.

Location filters work with both legacy search and Search API v2. They use Immich's location metadata, not a distance or radius around a map point. Leave all three fields blank to preserve the existing playlist behavior.

## Profile planning

The channel and screensaver share the same companion URL, but they can use different profiles.

That means you can:

- use the same profile for both
- give the screensaver its own profile
- keep the normal channel tuned for interactive use and the screensaver tuned for quieter background playback

![Roku settings dialog](./assets/screenshots/config-dialog.png){ .doc-screenshot .on-glb }
<p class="doc-caption">Use the Roku settings menu to refresh, switch profile, change companion, or clear cache.</p>

For the actual Roku-side behavior and menu actions, see [Using the Roku Apps](./roku-apps.md).

## Schedules

Profiles define photos, transitions and display settings. Schedules are separate, reusable objects that choose which profile plays when. Open **Schedules** in the companion, create a named schedule, choose its time zone and fallback profile, then add weekly time slots referencing existing profiles.

On Roku, choose a **Profile** for fixed playback or a **Schedule** for automatic switching. The channel and screensaver can follow different selections. Choosing a fixed profile stops following the schedule until you select the schedule again.

Outside the time slots, the fallback profile plays. Disabling time slots also uses the fallback. Each slot uses its referenced profile's content and display settings. Overlapping slots use the first matching row; use **Move up** to change priority. A schedule can reference profiles, including its fallback profile, but cannot reference another schedule.

Start times are included and end times are excluded. An earlier end time runs into the next day: a Friday 22:00–06:00 slot ends on Saturday morning. Equal start and end times mean 24 hours from the starting day. Slots follow local time in the chosen zone, including daylight saving changes.

Schedules are checked each minute while playback runs. A paused slideshow waits to switch until playback resumes; old queued switches expire and are checked again. If a target playlist is empty or unavailable, the current slideshow continues and the Roku retries. If a referenced profile is deleted, the fallback is used. If both are unavailable, the Roku keeps its cached playback when possible. When the companion is offline, the Roku keeps its last configuration; schedule changes require the companion.

Schedules are saved separately in the companion's `schedules/` data folder. Deleting a schedule does not delete its profiles. If you delete the schedule currently selected on a Roku, choose another schedule or profile on that Roku.
