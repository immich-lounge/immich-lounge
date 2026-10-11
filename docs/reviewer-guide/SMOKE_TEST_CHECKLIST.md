# Smoke Test Checklist

## Companion

- Open the web UI
- Save connection settings
- Test the Immich connection
- Create a profile
- Edit a profile and save changes
- Refresh a playlist

## Channel

- Enter the companion URL manually
- Select a profile
- Verify photos load
- Verify overlays render
- Verify weather works when enabled
- Verify settings can change the profile
- Verify settings can change the companion

## Screensaver

- Start with only a channel profile configured
- Verify the screensaver uses the channel profile
- Set a separate screensaver profile
- Verify the screensaver uses the new profile
- Verify cached profile and playlist fallback work

## Photos with any people

Use a dedicated companion test instance and a mixed selection with at least one photo that has a visible face in Immich and one that has no face. Include an unnamed face if available. Do not edit production profiles or persistent automated fixtures. Wait for any other Roku test session to release the device before sideloading or sending remote-control commands.

- On Immich 3.2+, enable **Photos → Filters → Only photos with people**, leaving specific people unselected. Preview and save; verify only face-bearing photos remain in the API playlist.
- Verify the flag survives reload and changing it invalidates the preview. With it off, both face-bearing and face-free photos should be available again.
- Verify album/tag selections, nested rules, locations, dates, file size, and people exclusions still narrow the results. Repeat with a shared album when available.
- Include On This Day and birthday memories where available; check face-free memories are removed and matching memories keep captions and album labels. Memories remain outside the regular date restriction.
- On Immich 3.1, verify new selections are unavailable and an already-enabled profile can be turned off. Previewing the enabled filter must report an unsupported-version error, never an unfiltered match count.
- On the physical Roku, play the filtered profile in the channel and screensaver. Confirm photos load and advance, the requested asset IDs match the filtered playlist, and the face-free control is never requested.
- Test a face-free-only selection: the Roku should show the empty-playlist state without playing a stale control photo. Switch back to the mixed filtered profile and verify playback resumes.
- Restore the previous sideloaded package and playback configuration after testing. Keep device console and companion request evidence under ignored build output.
