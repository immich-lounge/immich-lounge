# AGENTS.md

## Project Overview

immich-lounge is a two-component system: a **companion service** (ASP.NET Core + Blazor Server) and a **Roku channel/screensaver** (BrightScript/BrighterScript). The companion handles configuration and playlist building; the Roku fetches media directly from Immich.

## Repo Conventions

- In user-facing copy, use the product name **Immich Lounge**. Keep lowercase `immich-lounge` only for technical slugs such as URLs, repo names, package names, Docker tags, image names, and protocol identifiers.
- The GitHub organization and GHCR owner are `immich-lounge`. Public docs should use `https://github.com/immich-lounge/immich-lounge` and `ghcr.io/immich-lounge/immich-lounge-companion`.
- The companion defaults to port `4383` for local dev, Docker, and manual Roku setup.
- Keep public docs and README copy concise and plain. Avoid hype, filler, and over-marketing language.
- Public docs use **Zensical** with the existing `mkdocs.yml` compatibility path, not Jekyll. Main public routes are `/support`, `/privacy`, and `/tos`.
- Public website content lives under `docs/website/`. Reviewer and maintainer docs live under `docs/reviewer-guide/` and `docs/maintainer/`.
- Public docs should prefer the website URL `https://immich-lounge.github.io/` when linking users outward.
- Keep the README product-first. Public setup and product documentation should usually live in `docs/website/` or `CONTRIBUTING.md`, not in the README.
- When a change affects user-visible behavior, setup, settings, or other public-facing workflows, update the relevant docs in the same change unless the deferral is explicit.
- Public setup docs should describe the manual companion URL flow. Do not describe SSDP/discovery as a supported public setup path, and do not recommend Docker host networking in public docs.
- The Roku code uses split registry keys for channel and screensaver state. Do not reintroduce old public docs that describe a single `profileId` / `cachedProfile` / `cachedPlaylist` model.
- The companion Docker runtime image should use the chiseled extra ASP.NET image.
- For Roku quality checks, use Roku's official Static Analysis Tool (`roku/scripts/run-static-analysis.ps1`, `npm run analyze*`) plus `@rokucommunity/bslint`. The official tool is the certification-facing check; `bslint` is the fast local/CI preflight linter.
- Keep a technical changelog in `CHANGELOG.md` and a user-facing release summary in `docs/website/changelog.md`. When one is updated for a release, keep the other in sync and link between them.
- Put non-source local build output under `_out/` whenever possible. Website builds go to `_out/website/`; ad hoc local or agent-generated output should prefer a dedicated subfolder such as `_out/codex/`.
- Treat `companion/src/ImmichLoungeCompanion/artifacts/`, `tools/**/bin/`, `tools/**/obj/`, and generated build output as disposable and not source.
- Keep local planning/spec work under `.planning/`, not under `docs/`.
- `.planning/` is local-only and gitignored. Do not commit it or move its contents back into the public docs unless explicitly asked.
- Treat untracked local tooling folders such as `.claude/`, `.superpowers/`, `.playwright-mcp/`, and `tools/` carefully. Do not commit or clean them up unless explicitly requested.
- Branding SVGs live in `branding/`. To regenerate PNGs and deploy them to `roku/images/`, `docs/website/assets/`, and the companion wwwroot, run `node branding/render-branding.mjs`. This requires **Inkscape** (`C:\Program Files\Inkscape\bin\inkscape.exe`). Do not use other SVG-to-PNG tools (cairosvg, resvg, etc.) — they produce poor output for these assets. After running, commit the updated PNGs alongside any SVG changes.

---

## Companion Service (`companion/`)

**Tech stack:** .NET 10, ASP.NET Core, Blazor Server, MSTest, NSubstitute

### Commands (run from `companion/`)

```bash
# Run locally (dev port 4383)
dotnet run --project src/ImmichLoungeCompanion

# Run all tests
dotnet test --project tests/ImmichLoungeCompanion.Tests/ImmichLoungeCompanion.Tests.csproj

# Run a single test class or method
dotnet test --project tests/ImmichLoungeCompanion.Tests/ImmichLoungeCompanion.Tests.csproj --filter "FullyQualifiedName~PlaylistCache"

# Build Docker image
docker build -t immich-lounge-companion .

# Deploy
docker-compose up -d
```

### Key Architecture

- **`Api/`** — MVC controllers (`ProfilesController`, `PlaylistController`, `SettingsController`, `ImmichController`) expose the REST API consumed by the Roku
- **`Components/Pages/`** — Blazor Server pages: `Connection.razor`, `Profiles.razor`, `ProfileEditor.razor`
- **`Playlist/`** — `PlaylistCacheWorker` (hosted service), `PlaylistCache`, `PlaylistBuilder`; cache lifecycle: cold → `{ building: true }`, warm → served immediately, proactive rebuild 20% before expiry
- **`Storage/`** — `JsonSettingsRepository` and `JsonProfileRepository` write to `/data/settings.json` and `/data/profiles/<id>.json`
- **`Immich/`** — `ImmichClient` calls Immich REST API for search and memories
- **`Services/CompanionState.cs`** — Scoped Blazor circuit state

**DI lifetime rules (important):** `ImmichClient` and `PlaylistCacheWorker` are registered as **Singletons** — never change to Scoped, as `PlaylistCacheWorker` depends on `IImmichClient` and a Singleton cannot capture a Scoped service. `CompanionState` is **Scoped**.

**Profile security model:** Profile JSON files on disk do **not** contain the Immich API key. The API key is injected at serve time from `settings.json`, so the Roku receives a single enriched JSON object.

**Test framework:** MSTest + NSubstitute + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory). `Program` is declared `public partial class` to allow `WebApplicationFactory<Program>` in tests.

---

## Roku Channel/Screensaver (`roku/`)

**Tech stack:** BrighterScript (compiles to BrightScript), SceneGraph, Rooibos v5 test framework

### Commands (run from `roku/`)

Deployment requires env vars: `ROKU_HOST` (device IP) and `ROKU_PASSWORD` (dev mode password).

```bash
# Build only (outputs to _out/build/channel/)
npm run build

# Build screensaver variant (outputs to _out/build/screensaver/)
npm run build:screensaver

# Build and sideload channel to Roku
npm run deploy

# Build and sideload screensaver to Roku
npm run deploy:screensaver

# Build, deploy, and wait for Rooibos test completion (streams Roku console output)
npm run test

# Build and launch the Rooibos test app only
npm run test:deploy

# List discovered test suites/cases from local spec files
npm run test:list

# Open Roku debug telnet console (port 8085) — shows print/log output in real time
npm run telnet
```

> **Note:** Roku deploy and launch scripts are env-var-driven. Use `.env` plus the `roku-deploy*.json` configs for local device-specific deployment.

### Key Architecture

- **`source/`** — main entry point (`main.brs`), `AppController.brs`, `HttpClient.brs`, `Registry.brs`, `Utils.brs`; `test_main.brs` is excluded from production builds and swapped in for test builds; `main_screensaver.brs` is the screensaver entry point
- **`components/`** — SceneGraph XML + BrightScript pairs: `SlideshowScene`, `DiscoveryScene`, `SettingsScene`, `PlaylistTask`, `DiscoveryTask`, `ImageLoaderTask`, `WeatherTask`, `WeatherComponent`, `OverlayComponent`, `ToastComponent`, `ProgressBar`, `ProfileSelector`, `ManualEntryForm`, `CompanionSelector`, `AssetMetaTask`, `CompanionApiTask`
- **`tests/`** — Rooibos v5 spec files (`.spec.bs`); tests run **on the physical Roku device** via `npm run test`
- `npm run test` should stream the Roku console, wait for `[Rooibos Result]` / `[Rooibos Shutdown]`, and fail on timeout. Use `npm run test:deploy` only when you intentionally want a launch-only workflow.

**Three bsconfig files:**
- `bsconfig.json` — production channel build; excludes `test_main.brs`
- `bsconfig.screensaver.json` — production screensaver build
- `bsconfig.test.json` — test build; swaps `test_main.brs` → `source/main.brs`, includes Rooibos plugin and all `tests/**/*.spec.bs`

**Channel vs screensaver boundary:** Keep the Roku channel and screensaver behaviorally aligned where possible, but preserve separate startup/settings/input flows when Roku screensaver certification rules require it. Prefer sharing helper logic, playback behavior, and reusable UI pieces behind that boundary instead of recombining the app flows themselves. Practical screensaver limits in this repo currently include avoiding channel-style interactive setup flows, deep-link behavior, and app-driven input handling in the screensaver package. Re-check Roku's official developer docs before changing that boundary: https://github.com/rokudev/dev-doc

**Screensaver settings wiring:** The official Roku screensaver settings entry must launch the standalone screensaver configuration flow directly from `source/main_screensaver.brs` using `DiscoveryScene` in screensaver mode. Do not point Roku's screensaver settings back to the channel, and do not reintroduce placeholder settings scenes for the packaged screensaver flow. When changing screensaver startup/build wiring, keep `npm run check:screensaver-settings` passing and rerun Roku static analysis for both channel and screensaver.

**Build output convention:** Keep generated Roku build artifacts under `roku/_out/` only. The current staging dirs are `_out/build/channel`, `_out/build/screensaver`, and `_out/build/test`; roku-deploy temp dirs also live under `_out/.roku-deploy-*`. Treat all of these as disposable generated output and keep them out of git.

**Rooibos test tags:** Integration tests are tagged `!integration` and excluded from the default test run.

**Roku developer documentation:** https://github.com/rokudev/dev-doc — reference for SceneGraph nodes, BrightScript APIs, manifest fields, and channel certification requirements.

**Registry budget:** The Roku registry is limited to ~16 KB per app. The current keys are split by mode: shared `companionUrl`, plus `channelProfileId` / `screensaverProfileId`, `cachedChannelProfile` / `cachedScreensaverProfile`, and `cachedChannelPlaylist` / `cachedScreensaverPlaylist`. Cached playlists are truncated before being written.

**Startup/fallback flow:** The app reads `companionUrl` plus the selected profile for the current mode → fetches the enriched profile from the companion → fetches the playlist → starts the slideshow. If the companion is offline it falls back to the cached profile for that mode; if Immich is offline it falls back to the cached playlist for that mode. >20 consecutive asset load failures pause the slideshow with a 60-second countdown.

**Design target — FHD 1080p:** All UI coordinates and sizes are designed for 1920×1080. Roku auto-scales to 720p HD (÷1.5) and 480p SD (÷3), so use a **3-pixel grid**: every x/y position and width/height must be divisible by 3. This ensures pixel-perfect integer results at all three resolutions. Example: use 96px safe-zone margins (96/3=32 at SD), not 100px (100/3=33.3 fractional).

---

## Data Directory Layout

```
/data/
  settings.json          # Global: Immich URL, API key, friendly name, UUID
  profiles/<id>.json     # Per-profile config (no API key)
  cache/<id>.json        # Cached shuffled playlist (optional persistence)
```
