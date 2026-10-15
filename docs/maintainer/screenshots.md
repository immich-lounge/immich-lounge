# Application screenshots

Use real application screens with curated demo photos, named profiles, and a weekly schedule. Keep connection and maintenance screens in setup guides; the homepage should show playback, choosing photos, appearance, profiles, and scheduling.

## Capture notes

- Use the dedicated [development Immich server](immich-development.md) and a separate companion data directory. Keep fixture accounts and existing device settings unchanged.
- The companion captures show real album selections and matching photo previews. The connection form uses the documented example address without saving or testing it.
- Roku captures use the current SceneGraph UI with bundled demo photos and sanitized metadata. Registry writes were disabled during capture. These images demonstrate the UI, not network startup or authentication; the normal channel was restored afterward.
- Inspect images at full size for loading messages, pause indicators, clipped controls, and empty previews. Keep profile names consistent across companion and Roku images.
- Store screenshots in `docs/website/assets/screenshots/`. Update the README, website references, and captions together; captions should describe what is visible.
- Build with `zensical build --clean` and check desktop and mobile layouts before publishing. Keep ad hoc capture tooling, original photo downloads, and credentials local and ignored.

## Photo sources

Demo photos are used under the [Pexels License](https://www.pexels.com/license/), checked on 15 October 2026. Original downloads are retained only in ignored local output.

| Demo photo | Photographer | Source |
|---|---|---|
| Mountain lake | Bri Schneiter | [Calm Body of Lake Between Mountains](https://www.pexels.com/photo/calm-body-of-lake-between-mountains-346529/) |
| Alpine trail | Tom Schönmann | [Landscape with a Mountain Lake](https://www.pexels.com/photo/landscape-with-a-mountain-lake-18430361/) |
| Mountain reflection | lil artsy | [Reflection of a Mountain on a Lake](https://www.pexels.com/photo/reflection-of-a-mountain-on-a-lake-4390125/) |
| Waterfall | Jonathan Borba | [Waterfall on Cliff in Wild Nature](https://www.pexels.com/photo/waterfall-on-cliff-in-wild-nature-11266861/) |
