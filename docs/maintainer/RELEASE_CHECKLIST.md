# Release Checklist

## GitHub

- Push `master`
- Confirm CI passes
- Confirm Pages deploy passes
- Confirm the docs site renders correctly
- Confirm the GHCR image publishes and can be pulled

## Companion

- Start the latest image with `./companion-data:/data`
- Open `http://<host-ip>:4383`
- Verify `http://<host-ip>:4383/healthz`
- Verify existing settings and profiles survive a container update

## Roku

- Verify `Immich Lounge` installs and launches
- Verify `Immich Lounge Screensaver` installs and launches
- Run `npm run analyze` and `npm run analyze:screensaver`, or run Roku's official Static Analysis Tool directly for both packaged apps, and review the findings
- Verify first-time setup
- Verify changing profile
- Verify changing companion
- Verify screensaver falls back to the channel profile when no screensaver profile is set
- Verify offline fallback behavior
- Verify a longer slideshow run for stability

## Store Metadata

- Final app descriptions
- Final screenshots and artwork
- Final privacy URL
- Final support URL
- Final terms URL
- Final signing and encryption setup
