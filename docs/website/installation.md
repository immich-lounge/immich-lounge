---
icon: material/docker
---

# Installation

## Companion

If you already run immich with Docker Compose, the simplest setup is to add one service to that existing compose file.

Use a normal Docker port mapping on `4383`. Do not expose the companion to the public internet.

## Preferred setup

- Add the service to your existing Immich `docker-compose.yml`.
- Keep the companion in the same trusted LAN as your Roku and immich server.
- Keep `/data` persisted with a Docker volume.

Reference file:
[docker-compose.yml](https://github.com/immich-lounge/immich-lounge/blob/master/docker-compose.yml)

Copy/paste snippet:

```yaml title="docker-compose.yml"
--8<-- "https://raw.githubusercontent.com/immich-lounge/immich-lounge/master/docker-compose.yml"
```

```bash
docker compose up -d
```

The companion stores settings, profiles, and cache data under `/data`.

To update later:

```bash
docker compose pull
docker compose up -d
```

## Roku apps

Add the Roku apps to your account:

<div class="roku-links-card">
  <table class="roku-links-table">
    <thead>
      <tr>
        <th>App</th>
        <th>Channel Store</th>
        <th>Add Link</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>Immich Lounge</td>
        <td><a class="roku-link-pill" href="https://channelstore.roku.com/details/b07c35a07c79ab9a68c29c923ac481ce:bdf9083c719f8f235036b2c91bcef535/immich-lounge">Open in Store</a></td>
        <td><a class="roku-link-pill roku-link-pill-secondary" href="https://my.roku.com/account/add/immichlounge">Add to Account</a></td>
      </tr>
      <tr>
        <td>Immich Lounge Screensaver</td>
        <td><a class="roku-link-pill" href="https://channelstore.roku.com/details/1dbc550b0d535eebf60d6fc4b77ec0fb:a6fafa094f6a14e19c10f0bb1e06e315/immich-lounge-screensaver">Open in Store</a></td>
        <td><a class="roku-link-pill roku-link-pill-secondary" href="https://my.roku.com/account/add/immichloungesaver">Add to Account</a></td>
      </tr>
    </tbody>
  </table>
</div>

Install on your Roku:

- **Immich Lounge**
- **Immich Lounge Screensaver**

The channel handles normal playback. The screensaver is configured from Roku system settings.

## First Roku setup

Open **Immich Lounge** on Roku and enter the companion URL:

```text
http://192.168.1.10:4383
```

Then choose a profile.

![Roku companion connection screen](./assets/screenshots/change-companion.png){ .doc-screenshot }
<p class="doc-caption">Manual companion setup on Roku uses the companion host and port <code>4383</code>.</p>

## Screensaver setup

Go to Roku Settings, choose **Immich Lounge Screensaver**, then open **Configure Screensaver**.

The screensaver can share the same companion URL and use either the same or a different profile.

## Network notes

You usually only need two paths to work:

- Roku to companion on port `4383`
- Roku to immich on your immich server port, usually `2283`

The Roku fetches media and most metadata directly from immich. The companion is not a media proxy.

## After install

- Use [Configuration](./configuration.md) to understand the available connection and profile settings.
- Use [Using the Companion](./companion-app.md) for the normal companion workflow after setup.
- Use [Using the Roku Apps](./roku-apps.md) for first-run flow, remote controls, and the Roku settings menu.
