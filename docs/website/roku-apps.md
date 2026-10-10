---
icon: material/television-play
---

# Using the Roku Apps

This page covers the day-to-day Roku experience after the companion and profiles are already set up.

For companion setup and profile editing, see [Getting Started](./getting-started.md), [Configuration](./configuration.md), and [Using the Companion](./companion-app.md).

## Channel vs Screensaver

The Roku channel is used for setup, profile selection, and normal playback.

The Roku screensaver uses the same companion connection, but it behaves a little differently:

- It does not show the discovery or manual setup flow.
- If no screensaver profile is selected yet, it falls back to the channel profile.
- If no profile is configured at all, it shows a setup reminder and exits.
- If the companion is rebuilding a playlist and a cached playlist exists, the screensaver uses the cached playlist immediately.

## First-Run Flow

1. Open **Immich Lounge** on your Roku.
2. Enter the companion host and keep port `4383`.
3. Select the profile you want for the channel.
4. Start playback and confirm the slideshow is working.
5. If you also want the screensaver, go to `Settings` -> `Theme` -> `Screensavers` and switch the active screensaver to **Immich Lounge Screensaver**.
6. After it is selected, open **Configure Screensaver** to choose the companion and profile for screensaver mode.

![Roku companion connection screen](./assets/screenshots/change-companion.png){ .doc-screenshot }
<p class="doc-caption">Connect the Roku to the companion with the host address and default port <code>4383</code>.</p>

![Roku profile selection screen](./assets/screenshots/change-profile.png){ .doc-screenshot }
<p class="doc-caption">Choose the Roku profile after the companion returns the available list.</p>

## Change the Roku Screensaver

Use Roku system settings to change which screensaver the device runs:

1. Go back to the Roku home screen.
2. Open **Settings**.
3. Open **Theme**, then **Screensavers**.
4. Set the active screensaver to **Immich Lounge Screensaver**.
5. Open **Configure Screensaver**.
6. Confirm the companion URL, then choose the profile you want the screensaver to use.

Roku's support article for changing screensavers is here: [How to change the screensaver on your Roku streaming device](https://support.roku.com/article/212015418).

If **Immich Lounge Screensaver** is already selected, you only need **Configure Screensaver** to switch it to a different companion or profile.

## Remote Controls

In channel mode:

| Button | Action |
|---|---|
| `Left` | Previous photo |
| `Right` | Next photo |
| `OK` | Show or hide the overlay |
| `Up` | Show the overlay |
| `Down` | Hide the overlay |
| `Play/Pause` | Pause or resume playback |
| `*` | Open the settings menu |
| `Back` | Open the exit dialog |

In screensaver mode, Roku handles key presses at the system level and the screensaver exits instead of opening app controls.

## Roku Settings Menu

Press `*` during playback in the channel to open the settings dialog.

| Action | What it does |
|---|---|
| `Refresh Now` | Re-fetches the current profile and playlist immediately. |
| `Choose Profile or Schedule` | Keeps the companion URL and lets you pick a fixed profile or a named schedule for channel mode. Selecting a fixed profile stops following a schedule. |
| `Change Companion` | Switches the Roku to a different companion host. |
| `Clear Cache` | Removes cached profile and playlist data for the current mode. |

![Roku settings dialog](./assets/screenshots/config-dialog.png){ .doc-screenshot }
<p class="doc-caption">The Roku settings dialog is available from the <code>*</code> button during channel playback.</p>

The Roku also checks the active profile configuration about every 5 minutes and applies display, clock, and weather changes without replacing the current playlist. Use **Refresh Now** when you intentionally want to re-fetch both the profile and playlist immediately.

If an empty playlist gains photos, playback starts when the next playlist refresh finds them. A paused channel waits until you resume. This also works when a previously playing playlist becomes empty and later gains photos again.

Offline fallback uses only cached data from the selected companion and profile. Changing a profile clears that mode's cache and resume position; changing the shared companion clears both modes. If the screensaver inherits the channel profile, changing that profile also clears the screensaver cache. The new selection must load successfully before it is available offline.

When matching cached settings and photos are available, both apps wait up to two seconds for settings and three seconds for a playlist before using the cache. Photos normally appear in about five seconds plus image-loading time when the companion is unavailable. Without a usable cache, startup keeps the longer connection and playlist-building wait. Playback checks the companion again through its normal refresh cycle.

After updating from a version that did not record the cache's companion and profile, open each Roku app while the companion is reachable to refresh its offline cache.

## Typical Setup Patterns

- Use one profile for the channel and another for the screensaver if you want different rooms or moods.
- Use the channel for setup and troubleshooting, even if your main goal is the screensaver.
- If the companion host changes, use **Change Companion** from the Roku settings menu instead of reinstalling the apps.

## When to use the channel for troubleshooting

Prefer the normal channel when you need to:

- confirm the companion URL
- switch profiles
- force a refresh
- clear cached state

The screensaver is best treated as playback-only once it is configured.
