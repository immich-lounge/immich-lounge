# Live Immich search compatibility tests

Use the [persistent development setup](immich-development.md) for Docker prerequisites, accounts, container names, state migration, and lifecycle instructions. Accounts and mounted configuration live in the primary checkout's gitignored `.local/immich/`.

Run from the repository or feature worktree:

```powershell
# Start all three servers, prepare fixture metadata, and run all live checks.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Test

# Run only the 3.2 compatibility structured and legacy-fallback checks.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Test -Target Compatibility32
```

Each native server run performs 20 checks covering version selection, full pagination with a page size of one, country/state/city combinations, empty results, inclusive date boundaries, album intersections, scoped suggestions, restricted-key connection and read access, the actual playlist collector, and invalid-key errors.

Immich 3.2 and 3.3.1 repeat those checks with only its version probe hidden by a client handler. Searches and other calls reach the real server, verifying legacy fallback when version detection is unavailable. The total is 100 checks across 3.1, 3.2, and 3.3.1.

Four synthetic PNGs and two overlapping albums belong to the isolated fixture account. Setup waits for actual upload metadata extraction, then writes deterministic dates and locations to the dedicated database. These checks validate search and playlist behavior, not reverse-geocoding accuracy. People, tags, and memories receive live permission/read smoke checks rather than populated fixtures; memory location matching is covered by companion tests.

Repeated runs reuse accounts, asset IDs, and albums. Manual development photos belong in the separate administrator account. Reports are written to `.local/immich/v31-results.json`, `.local/immich/compat-v32-results.json`, and `.local/immich/development-results.json`; no production connection settings are changed.