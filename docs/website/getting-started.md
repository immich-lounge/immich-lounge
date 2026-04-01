---
icon: material/rocket-launch-outline
---

# Getting Started

This is the fastest path to a first working setup.

For a fuller Docker setup reference, Roku install links, and network notes, see [Installation](./installation.md).

You need:

- an **immich** server on your LAN
- an **immich API key**
- **Docker** on a machine that stays on
- a **Roku** on the same network

## 1. Start the companion

If you already use Docker Compose for immich, add the companion service from the repo-root `docker-compose.yml`.

```yaml title="docker-compose.yml"
--8<-- "https://raw.githubusercontent.com/immich-lounge/immich-lounge/master/docker-compose.yml"
```

```bash
docker compose up -d
```

This keeps the companion on port `4383` and persists its `/data` directory in a Docker volume.

## 2. Connect the companion to immich

Open:

```text
http://<your-docker-host-ip>:4383
```

On the Connection page, enter:

- a friendly name
- your immich server URL, for example `http://192.168.1.10:2283`
- an API key from immich

Then click **Test Connection** and save.

If you have not created the key yet, see [Immich API Key](./api-key.md).

![Immich Lounge companion connection page](./assets/screenshots/companion-connection.png){ .doc-screenshot }
<p class="doc-caption">Connect the companion to immich first. The Roku setup comes later.</p>

## 3. Create a profile

A profile decides what the Roku shows and how it should look.

Pick one or more content sources, then save the profile. The companion starts building the playlist in the background.

![Immich Lounge companion profile editor](./assets/screenshots/companion-profile-editor.png){ .doc-screenshot }
<p class="doc-caption">A profile controls content, slideshow behavior, overlays, and weather.</p>

## 4. Connect the Roku

Launch **Immich Lounge** on Roku, enter the companion URL, and choose a profile.

![Roku companion connection screen](./assets/screenshots/change-companion.png){ .doc-screenshot }
<p class="doc-caption">Enter the companion host and keep the default port <code>4383</code>.</p>

![Roku profile selection screen](./assets/screenshots/change-profile.png){ .doc-screenshot }
<p class="doc-caption">After the Roku reaches the companion, choose the profile you want to use for this device.</p>

If you also want the Roku screensaver, go to Roku Settings, choose **Immich Lounge Screensaver**, and open **Configure Screensaver**.

## API key notes

See [Immich API Key](./api-key.md) for the current immich steps to generate a key and the recommended permission set.

Immich Lounge does not create, update, or delete your immich library content.

## Next steps

- Use [Configuration](./configuration.md) to understand the available profile and connection settings.
- Use [Using the Companion](./companion-app.md) for the day-to-day companion pages after setup.
- Use [Using the Roku Apps](./roku-apps.md) for the channel controls, Roku settings menu, and screensaver behavior after setup.
