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
- the API key
- whether the companion host can reach immich on the network

## Photos are not loading

The most common causes are:

- Roku cannot reach immich
- `imageQuality` is set to `original` on slower hardware or networks
- too many consecutive asset load failures, which triggers the built-in retry pause

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
