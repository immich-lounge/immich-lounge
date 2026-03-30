---
icon: material/frequently-asked-questions
---

# FAQ

Answers to common setup and behavior questions.

---

## What does Immich Lounge do?

Immich Lounge turns a Roku into a photo display for your immich library. The companion handles configuration and playlist building. The Roku fetches media directly from immich.

---

## Do I need the companion service?

Yes. The Roku app depends on the companion for setup, profile selection, and playlist building.

---

## Does Immich Lounge store my photos?

No. Your photos stay in immich. The companion stores settings, profiles, and optional cached playlists. The Roku can cache profile and playlist data locally for offline fallback.

---

## What does the companion store?

The companion stores:

- Your immich server URL
- Your immich API key
- Profile configuration files
- Optional cached playlists

It does not copy your full photo library into the companion.

---

## Can the channel and screensaver use different profiles?

Yes. They share the same companion connection, but the Roku channel and Roku screensaver can use different selected profiles.

---

## Does the Roku fetch media from the companion?

No. The companion serves configuration and playlists. The Roku fetches media directly from immich using the enriched profile it receives from the companion.

---

## Does Immich Lounge need internet access?

For basic playback, the Roku needs LAN access to both the companion and your immich server. Internet access is only needed for features such as weather and for reaching public documentation links.

---

## Is manual companion setup supported?

Yes. Public setup uses the manual companion URL flow. Enter the companion host and port `4383` on the Roku, then select a profile.

---

## Does Immich Lounge support video playback?

Not yet. The current release is photo-only.

---

## Is Immich Lounge an official immich or Roku project?

No. Immich Lounge is an independent community project and is not affiliated with immich or Roku.
