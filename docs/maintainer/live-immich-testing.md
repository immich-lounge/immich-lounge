# Live Immich search compatibility tests

## Automated disposable checks

From the repository root, with the .NET 10 SDK and Docker running in Linux-container mode:

```bash
npm run test:database
```

Testcontainers starts a fresh isolated Immich stack for each pinned version: 3.1.0, 3.2.0,
and 3.3.1. It reuses the fixture seeding and search/playlist checks below, including the
legacy fallback checks on newer servers. Images are pinned by digest. Networks, database
and media volumes, accounts, and dynamically assigned ports are separate from the persistent
development stacks. Existing Immich settings and host database environment variables are unused.
Configuration is copied into containers, so no host bind mounts are required.

The fixture version is checked before account creation. A unique database marker is checked
before applying synthetic fixture SQL. Containers, volumes, and networks are disposed after
success or failure; leave the Testcontainers resource reaper enabled for cleanup after an
interrupted process. Docker retains downloaded images for later runs.

CI runs this as a separate integration job. The existing companion test command stays fast
and does not require Docker. TRX reports and attached check results or failure logs go under
`_out/testcontainers/lounge/`; account files and API keys are not uploaded.

This validates the companion's real Immich client and playlist collector. It does not start
the companion web service, replace production-image smoke tests, or test Roku playback,
screensaver behavior, people/tag matching, or device networking.

## Persistent manual checks

Use the [persistent development setup](immich-development.md) for Docker prerequisites, accounts, container names, state migration, and lifecycle instructions. Accounts and mounted configuration live in the primary checkout's gitignored `.local/immich/`.

Run from the repository or feature worktree:

```powershell
# Start all three servers, prepare fixture metadata, and run all live checks.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Test

# Run only the 3.2 compatibility structured and legacy-fallback checks.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Test -Target Compatibility32
```

Each native server run performs 20 checks covering version selection, full pagination with a page size of one, country/state/city combinations, empty results, inclusive date boundaries, album intersections, scoped suggestions, restricted-key connection and read access, the actual playlist collector, and invalid-key errors.

Immich 3.2 and 3.3.1 repeat those checks with only its version probe hidden by a client handler. Searches and other calls reach the real server, verifying legacy fallback when version detection is unavailable. No-face checks add explicit-false wire verification on supported versions and rejection when capability detection is unavailable, for 107 checks across 3.1, 3.2, and 3.3.1.

Disposable 3.2 and 3.3.1 tests additionally insert one synthetic visible face without a person assignment into the database guarded by that test's unique marker. They verify no-face exclusion, a with-people positive control, and album-scoped filtering. These are metadata fixtures with machine learning disabled, not tests of face-detection accuracy. Persistent fixture accounts and Compose databases are not modified by this extra check.

Four synthetic PNGs and two overlapping albums belong to the isolated fixture account. Setup waits for actual upload metadata extraction, then writes deterministic dates and locations to the dedicated database. These checks validate search and playlist behavior, not reverse-geocoding accuracy. People, tags, and memories receive live permission/read smoke checks rather than populated fixtures; memory location matching is covered by companion tests.

Repeated runs reuse accounts, asset IDs, and albums. Manual development photos belong in the separate administrator account. Reports are written to `.local/immich/v31-results.json`, `.local/immich/compat-v32-results.json`, and `.local/immich/development-results.json`; no production connection settings are changed.
