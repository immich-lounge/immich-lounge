---
icon: material/source-branch
---

# Architecture

## Component Overview

Main flow:

1. A browser connects to the companion service to configure settings and profiles.
2. The companion service stores settings, builds playlists, and exposes the setup API used by the Roku.
3. The Roku fetches the active profile and playlist from the companion.
4. The Roku then loads media directly from immich.
5. immich remains the source of truth for photos and metadata.

Weather path:

| Source | Destination | Purpose |
|--------|-------------|---------|
| Open-Meteo API | Roku device | Weather data polling for the optional on-screen weather display |

**Key principle:** The companion handles configuration and playlist building. The Roku handles media delivery and weather directly — the companion is never in those paths.

---

## Component 1: Companion Service

**Tech stack:** ASP.NET Core (.NET 10), Blazor Server, JSON file storage, Docker

The companion is a Docker container that runs on a machine on your LAN. It provides:

- **Web UI** — browser-based configuration via Blazor Server
- **REST API** — used by the Roku to fetch profiles and playlists
- **Playlist builder** — queries immich, deduplicates, shuffles, and caches the asset list

### Data Storage

```
/data/
  settings.json          # Global: immich URL, API key, friendly name, companion UUID
  profiles/
    living-room.json     # One file per profile (no API key — injected at serve time)
  cache/
    living-room.json     # Cached shuffled playlist (optional persistence)
```

### Enriched Profile Response

When the Roku fetches a profile, the companion **injects** the immich connection details into the response. This means:

- Profile files on disk do **not** contain the API key
- The Roku receives a single JSON object with everything it needs (including the API key for direct immich calls)
- The Roku caches this enriched profile locally for offline fallback

### Playlist Cache

The companion maintains one shuffled asset-ID list per profile. The cache lifecycle:

1. **Cold cache** — returns `{ building: true }` immediately; builds in background
2. **Warm cache** — returned immediately from memory (or disk if companion restarted)
3. **Proactive rebuild** — triggered before expiry (20% of interval, clamped to 1–10 minutes early)
4. **Invalidation** — immediately on profile update, profile delete, or global settings change

---

## Component 2: Roku Channel + Screensaver

**Tech stack:** BrightScript, SceneGraph, Roku OS 15.0+

The Roku project builds separate channel and screensaver variants. Both run the same display logic, but the channel mode has a richer setup flow.

### Startup Flow (Channel)

```
1. Read registry: companionUrl + selected profile ID
2. Missing? → Setup screen
3. Fetch enriched profile from companion
4. Success → save to the mode-specific cached profile registry key
5. Failure → load the mode-specific cached profile (or show error)
6. Fetch playlist (up to 500 entries)
7. Playlist building? → poll every 5s (up to 60s)
8. Success → hold in memory; save 100-entry truncated copy to registry
9. Failure → use the mode-specific cached playlist (or show error)
10. Start slideshow
11. Schedule periodic refresh (per refreshIntervalMinutes)
```

### Registry Keys (Roku)

| Key | Purpose | Max size |
|-----|---------|---------|
| `companionUrl` | Shared companion base URL | ~50 bytes |
| `channelProfileId` | Selected profile for the channel | ~70 bytes |
| `screensaverProfileId` | Selected profile for the screensaver | ~70 bytes |
| `cachedChannelProfile` | Cached enriched channel profile | ~4 KB |
| `cachedScreensaverProfile` | Cached enriched screensaver profile | ~4 KB |
| `cachedChannelPlaylist` | Cached channel playlist fallback | ~9.5 KB |
| `cachedScreensaverPlaylist` | Cached screensaver playlist fallback | ~9.5 KB |

The app keeps only one cached playlist per mode and truncates it before writing to the registry to stay within Roku's storage limits.

### Media URL Construction

The Roku builds media URLs directly from the immich server URL stored in the cached enriched profile:

| Asset type | URL |
|-----------|-----|
| Photo (preview) | `<immichUrl>/api/assets/<id>/thumbnail?size=preview` |
| Photo (original) | `<immichUrl>/api/assets/<id>/original` |
| Video | `<immichUrl>/api/assets/<id>/video/playback` |
| Live Photo | `<immichUrl>/api/assets/<livePhotoVideoId>/video/playback` |

All immich API calls include: `x-api-key: <apiKey>` header.

### Fallback Chain

| Condition | Behaviour |
|-----------|-----------|
| Companion offline | Use the cached profile for the current mode; toast "Using cached config (companion offline)" |
| immich offline | Use the cached playlist for the current mode; toast "Using cached playlist (immich offline)" |
| Per-slide metadata fail | Omit that overlay field for the slide |
| Individual asset load fail | Skip silently; increment failure counter |
| >20 consecutive failures | Pause slideshow; show 60-second countdown |
| No cache + companion offline | Full-screen error with setup hint |
| Profile deleted (404) | Toast; navigate to setup |

---

## Component 3: immich Server

The Roku interacts with immich directly using these endpoints:

| Call | Purpose |
|------|---------|
| `GET /api/assets/<id>` | Per-slide metadata (date, location, people, EXIF) |
| `GET /api/assets/<id>/thumbnail?size=preview` | Preview image |
| `GET /api/assets/<id>/original` | Original image |
| `GET /api/assets/<id>/video/playback` | Video/Live Photo playback |

The companion uses these endpoints for playlist building:

| Call | Purpose |
|------|---------|
| `POST /api/search/assets` | Search by album, person, or tag |
| `GET /api/memories?date=<date>` | Fetch "On This Day" memories |

---

## Security Model

Immich Lounge is designed for **trusted home networks only**:

- The companion web UI and API are **unauthenticated**
- The immich API key is stored in plaintext in the companion's data volume
- The enriched profile (including the API key) is cached in the Roku's registry
- **Do not expose the companion port (4383) to the internet**

Future: optional Basic Auth on the companion (out of scope for v1).
