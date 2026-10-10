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

After saving, use **Test Connection** to make sure the companion can reach Immich. The result also shows the server version and selected search API. Immich Lounge selects Search API v2 for stable Immich 3.2+ servers and legacy search for older servers. If version detection is unavailable, it uses legacy search with the same filters.

Detection refreshes automatically after five minutes; **Test Connection** refreshes it immediately. No settings or profile migration is needed.

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
| Content | Albums, people, tags, memories, and optional date and location filtering |
| Playback | Interval, shuffle, transition effect, photo motion, and playlist refresh interval |
| Display | Overlay style and fields, background effect, date and time formatting, weather, and image quality |

> **Shared albums:** Immich Lounge currently lists only albums owned by the configured Immich user. Shared albums are hidden because Immich search does not yet reliably return assets from shared albums. Track upstream support in the Immich discussion [Shared Albums in Search Results](https://github.com/immich-app/immich/discussions/2995).

![Immich Lounge slideshow with overlay, clock, and weather](./assets/screenshots/slideshow.png){ .doc-screenshot }
<p class="doc-caption">Example slideshow with a left-side overlay, persistent clock, and weather.</p>

When weather is enabled for a profile, you can choose whether the Roku shows temperatures in Celsius or Fahrenheit.

### Zoom to fit

In the profile's **Slideshow** settings, enable **Zoom to fit (landscape photos)** to fill the screen with landscape photos. This preserves the photo's proportions and crops the edges as needed. Portrait and square photos still show the whole image.

The setting is off by default and works in both the channel and screensaver. After the Roku refreshes the profile, it applies to the next photo displayed.

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

![Roku settings dialog](./assets/screenshots/config-dialog.png){ .doc-screenshot }
<p class="doc-caption">Use the Roku settings menu to refresh, switch profile, change companion, or clear cache.</p>

For the actual Roku-side behavior and menu actions, see [Using the Roku Apps](./roku-apps.md).
