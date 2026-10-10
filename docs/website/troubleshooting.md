---
icon: material/wrench-outline
---

# Troubleshooting

<div class="section-intro">
Most setup problems come down to one of three things: the Roku cannot reach the companion, the Roku cannot reach immich directly, or cached state is hiding a recent change.
</div>

## The Roku cannot connect to the companion

Check the companion URL first:

```text
http://192.168.1.10:4383
```

Make sure:

- the companion container is running
- the Roku and companion are on the same LAN
- port `4383` is reachable
- the URL includes `http://`

## The Roku says it is using cached config or cached playlist

That usually means one side of the startup path is offline:

- **cached config** usually means the companion could not be reached
- **cached playlist** usually means immich could not be reached directly from the Roku

Important detail: the Roku talks to immich directly for media and most metadata.

See [Using the Roku Apps](./roku-apps.md) for the difference between cached config, cached playlist, and the Roku menu actions that can refresh or clear state.

## The companion web UI does not load

Start with the easy checks:

- `docker compose ps`
- `docker compose logs`
- local firewall rules for port `4383`

## Test Connection fails in the companion

Check:

- the immich server URL, including port
- do not use `localhost`, `127.0.0.1`, `::1`, or wildcard bind addresses such as `0.0.0.0`
- the API key
- whether the companion host can reach immich on the network

If needed, generate a fresh key and confirm the recommended permissions from [Immich API Key](./api-key.md).

## Photos are not loading

The most common causes are:

- Roku cannot reach immich
- `imageQuality` is set to `original` on slower hardware or networks
- too many consecutive asset load failures, which triggers the built-in retry pause

If you see **Could not load the first photo. Retrying...** on an older Roku app, remove any trailing `/` from the Immich server URL in the companion, save it, and refresh the Roku configuration. For example, use `http://192.168.1.10:2283`. Newer builds remove trailing slashes automatically.

## Playlists fail to build with Memories enabled

Immich v3.0.0–v3.0.2 can reject the date used for **Include Memories (On This Day)**. Check the companion logs for an HTTP `400` on `GET /api/memories`. A successful **Test Connection** does not check this endpoint.

Immich fixed the date validation in [v3.0.3](https://github.com/immich-app/immich/releases/tag/v3.0.3). Upgrade to v3.0.3 or a newer release, then retry with Memories enabled. Until you can upgrade, disable **Include Memories (On This Day)** in the profile and save it to use the other content sources.

If it still fails after upgrading, check the companion logs for the failing endpoint and HTTP status before resetting your configuration.

## The clock, date, or weather is missing

- make sure those fields are enabled in the profile
- weather still needs internet access from the Roku
- clock and date now fall back locally if the companion date endpoint is unavailable

## Profile changes do not show up on Roku

The Roku refreshes on a schedule, so recent changes can take a while to appear.

To force a refresh:

1. Press `*` on the Roku remote.
2. Open settings.
3. Choose **Refresh Now**.

See [Using the Roku Apps](./roku-apps.md) for the rest of the channel controls and settings menu options.

## I want to reset everything

For the Roku, use **Clear Cache** from the Roku settings menu inside the app.

See [Using the Roku Apps](./roku-apps.md) for what `Clear Cache` affects on the device.

For the companion, stop it, remove `./companion-data`, and start it again:

```bash
docker compose down
docker compose up -d
```
