---
icon: material/television-play
---

# Using the Roku Apps

How the Roku channel and screensaver behave once setup is complete.

---

## Channel vs Screensaver

The Roku channel is used for setup, profile selection, and normal playback.

The Roku screensaver uses the same companion connection, but it behaves a little differently:

- It does not show the discovery or manual setup flow.
- If no screensaver profile is selected yet, it falls back to the channel profile.
- If no profile is configured at all, it shows a setup reminder and exits.
- If the companion is rebuilding a playlist and a cached playlist exists, the screensaver uses the cached playlist immediately.

---

## First-Run Flow

1. Open **Immich Lounge** on your Roku.
2. Enter the companion host and keep port `4383`.
3. Select the profile you want for the channel.
4. Start playback and confirm the slideshow is working.
5. If you also want the screensaver, enable **Immich Lounge** in Roku's screensaver settings.
6. In Roku's screensaver settings, open **Configure Screensaver** to choose the companion and profile for screensaver mode.

![Roku companion connection screen](./assets/screenshots/change-companion.png){ .doc-screenshot }
<p class="doc-caption">Connect the Roku to the companion with the host address and default port <code>4383</code>.</p>

![Roku profile selection screen](./assets/screenshots/change-profile.png){ .doc-screenshot }
<p class="doc-caption">Choose the Roku profile after the companion returns the available list.</p>

---

## Remote Controls

In channel mode:

- `Left` — previous photo
- `Right` — next photo
- `OK` — show or hide the overlay
- `Up` — show the overlay
- `Down` — hide the overlay
- `Play/Pause` — pause or resume playback
- `*` — open the settings menu
- `Back` — open the exit dialog

In screensaver mode, Roku handles key presses at the system level and the screensaver exits instead of opening app controls.

---

## Roku Settings Menu

Press `*` during playback in the channel to open the settings dialog. From there you can:

- Refresh the current playlist
- Change profile
- Change companion
- Clear cached data

![Roku settings dialog](./assets/screenshots/config-dialog.png){ .doc-screenshot }
<p class="doc-caption">The Roku settings dialog is available from the <code>*</code> button during channel playback.</p>

---

## Typical Setup Patterns

- Use one profile for the channel and another for the screensaver if you want different rooms or moods.
- Use the channel for setup and troubleshooting, even if your main goal is the screensaver.
- If the companion host changes, use **Change Companion** from the Roku settings menu instead of reinstalling the apps.
