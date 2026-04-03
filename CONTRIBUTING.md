# Contributing

Thanks for helping improve Immich Lounge.

## Before You Start

- Open an issue for bugs or feature ideas when possible.
- Keep changes focused. Small, clear pull requests are easier to review and ship.
- If your change affects user-facing behavior, update the docs too.

## Local Setup

For a reviewer stack that starts Immich and the local companion together in Docker, see [REVIEWER_SETUP.md](./docs/reviewer-guide/REVIEWER_SETUP.md).

### Companion

```bash
cd companion
dotnet restore src/ImmichLoungeCompanion/ImmichLoungeCompanion.csproj
dotnet run --project src/ImmichLoungeCompanion
dotnet test --project tests/ImmichLoungeCompanion.Tests/ImmichLoungeCompanion.Tests.csproj
```

For local Docker runs, use the repo-root local override:

```bash
docker compose -f docker-compose.local.yml build --no-cache
docker compose -f docker-compose.local.yml up -d --force-recreate
docker compose -f docker-compose.local.yml down
```

If you prefer the same shorthand used in immich-reversegeo, the repo root also includes:

```bash
npm run docker:build
npm run docker:up
npm run docker:down
```

The repo-root [`docker-compose.yml`](./docker-compose.yml) is the end-user reference snippet for published companion images. [`docker-compose.local.yml`](./docker-compose.local.yml) is the contributor-local variant that builds from the current source.

### Roku

```bash
cd roku
npm ci
npm run lint
npm run lint:screensaver
npm run build
npm run build:screensaver
npm run analyze
npm run analyze:screensaver
```

`npm run lint` runs `@rokucommunity/bslint` for the main channel, and `npm run lint:screensaver` runs it for the screensaver variant. This is a local and CI preflight check.

`npm run analyze` and `npm run analyze:screensaver` download and run Roku's official Static Analysis Tool against the built app folders. These commands require Java and internet access the first time they run.

The tool is available from Roku here:

- [Roku Static Analysis Tool](https://developer.roku.com/en-gb/docs/developer-program/dev-tools/static-analysis-tool/static-analysis-tool.md)
- [sca-cmd.zip](https://devtools.web.roku.com/static-channel-analysis/sca-cmd.zip)

I would treat the official Roku tool as the certification-facing check, while `bslint` stays the fast day-to-day linter.

For local device testing, Roku Developer Mode and sideloading are still used. Create a local `.env` file with:

```env
ROKU_HOST=...
ROKU_PASSWORD=...
```

Useful commands:

```bash
npm run deploy
npm run deploy:screensaver
npm run telnet
```

Official Roku developer docs:
- [Build a streaming app on the Roku platform](https://developer.roku.com/en-us/develop)
- [Activating developer mode](https://developer.roku.com/en-gb/docs/developer-program/getting-started/developer-setup.md)
- [developer.roku.com](https://developer.roku.com/)

## Pull Requests

- Describe what changed and why.
- Link related issues.
- Include screenshots or short videos for UI changes when helpful.
- Call out any follow-up work or known limitations.

## Style Notes

- Follow existing naming and structure in each app.
- Keep generated build output out of version control.
- Do not commit secrets, `.env` values, or device-specific local config.

## Docs

The documentation site is built with Zensical using the existing [`mkdocs.yml`](./mkdocs.yml) compatibility path. Public website content lives under [`docs/website/`](./docs/website/). The generated site output goes to `_out/website/`. If you add or rename public docs pages, update [`mkdocs.yml`](./mkdocs.yml) too.

To build or preview the docs locally, install the Python packages first:

```bash
py -m pip install -r docs/website/requirements.txt
```

Then use one of these:

```bash
py -m zensical serve
py -m zensical build
```

If you need temporary local build output that is not part of the normal project layout, prefer a dedicated folder under `_out/` instead of creating new top-level build directories.
