# Release Checklist

## Prepare the candidate

- Create a release branch from current `master`; keep unrelated checkout changes separate.
- Update both Roku manifests and the companion project version together. For 1.1.0, Roku uses `major_version=1`, `minor_version=1`, and `build_version=0000`.
- Move released entries into dated sections in both changelogs and keep their links intact.
- Run companion Release tests, Roku script tests, both builds/lints/static analyses, the physical-device suite, and the docs build.
- Run the Release workflow on the candidate branch with `release_name=v1.1.0` to build reviewable artifacts. This workflow does not publish a GitHub release or update the stable Docker tag.
- Prepare a draft GitHub release with notes, artifact checksums, and the exact candidate commit. Roku build ZIPs are unsigned; Store packaging/signing is a separate step.

## GitHub

- Merge the reviewed release PR into `master` and verify its exact commit.
- Confirm CI passes
- Confirm Pages deploy passes
- Confirm the docs site renders correctly
- Confirm the GHCR `edge` and commit-tagged image publishes and can be pulled.
- Run the Release workflow on the merged commit and replace draft assets if its source commit changed.
- Publish the draft release only after validation. Promote the companion to `latest` using Publish Docker Image with `publish_latest=true` on the verified commit.

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
