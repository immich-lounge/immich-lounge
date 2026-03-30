# Reviewer Setup

Use the reviewer stack if you want one Docker Compose file that brings up:

- Immich server
- Immich machine learning
- Postgres with `pgvecto-rs`
- Valkey
- the published Immich Lounge companion image on port `4383`

This is meant for reviewing the companion against a real Immich instance without installing Immich separately first.

## Prerequisites

- Docker Engine 24.0+ and Docker Compose v2
- enough free disk space for Immich uploads and Postgres data

## Start the stack

From the repo root, copy the reviewer environment file next to the compose file:

```bash
Copy-Item .\docs\reviewer-guide\.env.reviewer.example .\docs\reviewer-guide\.env.reviewer
```

Start everything from the repo root:

```bash
docker compose -f .\docs\reviewer-guide\docker-compose.reviewer.yml up -d --build
```

Open the apps:

- Immich: `http://localhost:2283`
- Immich Lounge companion: `http://localhost:4383`

## Default data directories

The compose file stores data under `./reviewer-data/` in the repo root:

- `./reviewer-data/immich-library`
- `./reviewer-data/postgres`
- `./reviewer-data/companion-data`

Delete that folder only if you want a full reset of the reviewer environment.

## Typical reviewer flow

1. Open Immich at `http://localhost:2283` and complete the first-run admin setup.
2. Upload a few photos from `docs/reviewer-guide/sample-media/` into Immich, or use your own test photos.
3. Open the companion at `http://localhost:4383`.
4. Enter the Immich URL as `http://immich-server:2283` in the companion settings.
5. Create a profile and continue with Roku testing.

## Quick Sample Media

If you just need a few assets for a fast smoke test, use the bundled images in `docs/reviewer-guide/sample-media/`. They are not imported automatically yet, but they give reviewers a small ready-to-upload set without needing personal photos.

## Notes

- The reviewer compose file and `.env.reviewer` live together under `docs/reviewer-guide/`.
- The compose file uses explicit relative paths back to the repo root for `reviewer-data/`.
- The reviewer stack uses the published `ghcr.io/immich-lounge/immich-lounge-companion` image so reviewers do not need a local companion build.
- Set `IMMICH_LOUNGE_TAG` in `.env.reviewer` if you want to review a specific published image tag instead of `latest`.
- This stack is for Immich plus the companion. Roku sideloading still uses the normal local `roku/` workflow and a physical Roku device.
- The Immich service layout follows Immich's current official Docker Compose guidance: [Docker Compose [Recommended]](https://docs.immich.app/install/docker-compose).
