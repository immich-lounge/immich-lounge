---
description: "Install the companion with Docker Compose and add the Immich Lounge channel and screensaver to Roku."
icon: material/docker
---

# Installation

Check the [requirements](./index.md#requirements) before installing. Immich Lounge supports Immich 3.1 or later; birthday memories require Immich 3.3 or later.

## Companion

If you already run immich with Docker Compose, the simplest setup is to add one service to that existing compose file.

Use a normal Docker port mapping on `4383`. Do not expose the companion to the public internet.

## Preferred setup

Choose the Compose layout that fits your installation. Keep the companion on the same trusted LAN as your Roku and Immich server, and persist its data in a Docker volume.

!!! warning "Keep the companion on your LAN"

    Use a normal Docker port mapping on `4383`. The companion does not provide public-internet authentication.

=== "Existing Immich stack"

    Add the service and named volume below to your existing Immich Compose file. Keep its existing services and volumes.

    ```yaml title="Companion service and volume"
    --8<-- "docker-compose.yml"
    ```

    From that stack's folder, start only the companion:

    ```bash
    docker compose up -d immich-lounge-companion
    ```

=== "Separate companion stack"

    Create a folder for the companion and save this as `docker-compose.yml`:

    ```yaml title="docker-compose.yml"
    --8<-- "docker-compose.yml"
    ```

    From that folder, start the companion:

    ```bash
    docker compose up -d
    ```

The companion stores settings, profiles, schedules, and cache data under `/data`.

??? info "Why localhost does not work for the Immich URL"

    The companion and Roku both need to reach the Immich server. `localhost` refers to the device making the request, so it does not identify your Immich server from the Roku. Use its LAN host name or IP address.

To update the companion later, run these from its Compose folder:

```bash
docker compose pull immich-lounge-companion
docker compose up -d immich-lounge-companion
```

Reference: [docker-compose.yml](https://github.com/immich-lounge/immich-lounge/blob/master/docker-compose.yml).


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

![Roku companion connection screen](./assets/screenshots/change-companion.png){ .doc-screenshot .on-glb }
<p class="doc-caption">Manual companion setup on Roku uses the companion host and port <code>4383</code>.</p>

## Screensaver setup

From the Roku home screen, go to `Settings` -> `Theme` -> `Screensavers`, switch the active screensaver to **Immich Lounge Screensaver**, then open **Configure Screensaver**.

The screensaver can share the same companion URL and use either the same or a different profile.

For the step-by-step Roku flow, see [Using the Roku Apps](./roku-apps.md#change-the-roku-screensaver) and Roku's own help article: [How to change the screensaver on your Roku streaming device](https://support.roku.com/article/212015418).

## Network notes

You usually only need two paths to work:

- Roku to companion on port `4383`
- Roku to immich on your immich server port, usually `2283`

The Roku fetches media and most metadata directly from immich. The companion is not a media proxy.

## After install

- Use [Configuration](./configuration.md) to understand the available connection and profile settings.
- Use [Using the Companion](./companion-app.md) for the normal companion workflow after setup.
- Use [Using the Roku Apps](./roku-apps.md) for first-run flow, remote controls, and the Roku settings menu.
