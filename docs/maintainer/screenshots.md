# Application screenshots

Use the current application UI with curated demo photos, named profiles, and a weekly schedule. Keep connection and maintenance screens in setup guides; the homepage should show playback, choosing photos, appearance, profiles, and scheduling.

## Demo data and companion captures

Start the dedicated development Immich server as described in [Immich development](immich-development.md). Then run from the primary checkout root, where the persistent `.local/immich/` account files live:

```powershell
node scripts/screenshots/prepare-demo.js
dotnet build companion/src/ImmichLoungeCompanion/ImmichLoungeCompanion.csproj --no-restore -o _out/codex/screenshots/app
dotnet _out/codex/screenshots/app/ImmichLoungeCompanion.dll --urls=http://127.0.0.1:4463 --ASPNETCORE_URLS=http://127.0.0.1:4463 --DataDirectory="$PWD/_out/codex/screenshots/data" --contentRoot="$PWD/companion/src/ImmichLoungeCompanion" --environment=Development
```

In another terminal, run `node scripts/screenshots/capture-companion.js`. It requires Playwright and its Chromium browser. If using an existing installation, set `PLAYWRIGHT_MODULE` to its module path and `SCREENSHOT_BROWSER` to the browser executable. No new repository dependency is required.

The preparation script adds four demo photos and three dedicated albums to the development administrator account. It leaves the fixture account unchanged. Upload IDs and the demo read key are retained in `.local/immich/screenshots-demo.json`; companion configuration and captures are under `_out/codex/screenshots/`. Rerunning preparation reuses the uploads and resets only the dedicated companion demo profiles and schedule.

The browser captures the real Blazor UI and real match previews. The connection screenshot shows the documented example address in the editable form without saving it or contacting that address. It does not imply a successful connection test. Tall captures use a taller browser viewport so the sticky footer does not obscure content.

## Roku captures

```powershell
node scripts/screenshots/prepare-roku.js
# Only after agreeing to replace the installed development app:
node scripts/screenshots/capture-roku.js --replace-dev-app
```

The preparation script changes generated staging files under `roku/_out/screenshots/` only. It uses the current SceneGraph playback, profile selector, manual connection form, and settings UI. Selected demo photos and sanitized asset metadata are bundled in the package, replacing network transport for this capture session. No API key or development-server access is needed on Roku. Registry writes and deletes are disabled in the generated capture build to preserve saved setup, caches, and resume state.

This capture fixture demonstrates rendered UI; it is not a test of the normal network startup or Immich authentication flow. Never release or publish its package.

The capture script builds a normal channel restoration package before deployment and restores it in `finally`, returning Roku to Home. This restores the **current checkout's normal channel**, not the exact previously installed development app. Establish that replacement is acceptable before running it. Device credentials come from the existing `roku/.env` or environment variables.

## Review and promotion

- Inspect every capture at full size. Avoid pause indicators, test names, loading states, clipped controls, and empty photo previews.
- Use the same profile names across companion and Roku captures. Landscape playback and portrait pairing should each have a clear example.
- Copy approved companion PNGs and native Roku JPEGs from `_out/codex/screenshots/captures/` into `docs/website/assets/screenshots/`.
- Update affected README and website references and captions together. Captions should describe what is visible, including whether weather is actually enabled.
- Build the website with `zensical build --clean` and inspect desktop and mobile layouts. Do not publish as part of capture.
- Stop the dedicated companion after capturing. Retain `.local/immich/` for future updates.

## Photo sources

Demo photos are used under the [Pexels License](https://www.pexels.com/license/), checked on 15 October 2026. The selection manifest is [`scripts/screenshots/photos.json`](../../scripts/screenshots/photos.json). Source images are downloaded into ignored build output and are not added separately to the repository.

| Demo photo | Photographer | Source |
|---|---|---|
| Mountain lake | Bri Schneiter | [Calm Body of Lake Between Mountains](https://www.pexels.com/photo/calm-body-of-lake-between-mountains-346529/) |
| Alpine trail | Tom Schönmann | [Landscape with a Mountain Lake](https://www.pexels.com/photo/landscape-with-a-mountain-lake-18430361/) |
| Mountain reflection | lil artsy | [Reflection of a Mountain on a Lake](https://www.pexels.com/photo/reflection-of-a-mountain-on-a-lake-4390125/) |
| Waterfall | Jonathan Borba | [Waterfall on Cliff in Wild Nature](https://www.pexels.com/photo/waterfall-on-cliff-in-wild-nature-11266861/) |
