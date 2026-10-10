---
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

- friendly name
- immich server URL
- immich API key

Use an immich URL that the Roku can reach on your LAN. `localhost`, `127.0.0.1`, `::1`, and wildcard bind addresses are not valid here.

After saving, use **Test Connection** to make sure the companion can reach immich.

![Immich Lounge companion connection page](./assets/screenshots/companion-connection.png){ .doc-screenshot }
<p class="doc-caption">The connection page stores the single immich connection used by all profiles.</p>

> **Security note:** The companion is for trusted home networks. It does not provide public-internet authentication.

## Profiles

A profile decides what to show and how to show it. You can create as many as you want.

![Immich Lounge companion profiles page](./assets/screenshots/companion-profiles.png){ .doc-screenshot }
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
| Content | Albums, people, tags, memories, and optional date filtering |
| Playback | Interval, shuffle, transition effect, photo motion, and playlist refresh interval |
| Display | Overlay style and fields, background effect, date and time formatting, weather, and image quality |

> **Shared albums:** Immich Lounge currently lists only albums owned by the configured Immich user. Shared albums are hidden because Immich search does not yet reliably return assets from shared albums. Track upstream support in the Immich discussion [Shared Albums in Search Results](https://github.com/immich-app/immich/discussions/2995).

![Immich Lounge slideshow with overlay, clock, and weather](./assets/screenshots/slideshow.png){ .doc-screenshot }
<p class="doc-caption">Example slideshow with a left-side overlay, persistent clock, and weather.</p>

When weather is enabled for a profile, you can choose whether the Roku shows temperatures in Celsius or Fahrenheit.

### Zoom to fit

In the profile's **Slideshow** settings, enable **Zoom to fit (landscape photos)** to fill the screen with landscape photos. This preserves the photo's proportions and crops the edges as needed. Portrait and square photos still show the whole image.

The setting is off by default and works in both the channel and screensaver. After the Roku refreshes the profile, it applies to the next photo displayed.

### Side-by-side portraits

Enable **Show portrait photos side by side** in the profile's **Slideshow** settings to pair consecutive portrait photos. Both photos are shown in full for one slideshow interval. Landscape, square, and unmatched portrait photos show alone. Pairs stay within the current playlist window and do not cross its boundary.

The setting is off by default and works in both the channel and screensaver, including cached playlists. The overlay describes the left photo. Pairs fade together (or switch immediately if transitions are set to None); Ken Burns motion still applies to single photos.

If the right photo cannot load, the left shows alone and the right is retried as the next item. Changes apply to the next photo displayed after the profile refresh.

## Profile planning

The channel and screensaver share the same companion URL, but they can use different profiles.

That means you can:

- use the same profile for both
- give the screensaver its own profile
- keep the normal channel tuned for interactive use and the screensaver tuned for quieter background playback

![Roku settings dialog](./assets/screenshots/config-dialog.png){ .doc-screenshot }
<p class="doc-caption">Use the Roku settings menu to refresh, switch profile, change companion, or clear cache.</p>

For the actual Roku-side behavior and menu actions, see [Using the Roku Apps](./roku-apps.md).

## Profile schedules

In a profile's editor, enable **Profile schedule**, choose a time zone, and add time slots with a target profile and starting days. Select this profile on each Roku that should follow the schedule. The channel and screensaver can follow different profiles and schedules.

Outside the time slots, the selected profile's own content and settings apply. Each slot uses the target profile's content and settings, without following that target's schedule. Overlapping slots use the first matching row; use **Move up** to change priority.

Start times are included and end times are excluded. An earlier end time runs into the next day: a Friday 22:00–06:00 slot ends on Saturday morning. Equal start and end times mean 24 hours from the starting day. Slots follow local time in the chosen zone, including daylight saving changes.

Once loaded, a schedule is checked each minute while playback runs. Enabling a schedule on an already running profile can take up to five minutes to be detected. A paused slideshow waits to switch until playback resumes. If the target playlist is empty or unavailable, the current slideshow continues and the Roku retries. If a target profile is deleted, the selected profile is used instead. When the companion is offline, the Roku keeps its last configuration; schedule changes require the companion.
