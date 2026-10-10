# Persistent Immich development servers

Run dedicated local Immich servers for Immich Lounge development. This setup does not change the companion's connection settings.

| Target | Version | URL | Docker project |
|---|---|---|---|
| Development | 3.2.0 | http://127.0.0.1:13283 | immich-lounge-dev-immich |
| Legacy | 3.1.0 | http://127.0.0.1:12283 | immich-lounge-compat-v31 |

Requires Docker Desktop in Linux-container mode, Git, PowerShell 7, and the companion's .NET SDK. Start Docker Desktop, then run from the repository or a worktree:

```powershell
# Start both servers and prepare their isolated fixture accounts.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Start

# Start only the persistent development server.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Start -Target Development

# Inspect containers or stop the legacy server without removing data.
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Status
pwsh -NoProfile -File companion/tests/LiveImmich/Run-LiveImmich.ps1 -Action Stop -Target Legacy
```

The first start downloads pinned images. The development server restarts with Docker unless explicitly stopped; the legacy server remains opt-in. Services bind only to loopback, so these URLs are for this computer, not Roku setup. Machine learning and reverse geocoding are disabled for deterministic fixtures.

## Accounts and local state

Persistent state belongs in **the primary checkout's `.local/immich/`**, even when the script runs in a worktree. This directory is gitignored and survives removal of feature worktrees. It is not disposable build output.

- Open `ACCOUNTS.local.md` for URLs, account roles, and links to credentials. It contains no duplicated passwords or keys.
- `v32-account.json` and `v31-account.json` contain each server's administrator login, fixture login, restricted fixture API key, and fixture IDs.
- `v32.env` and `v31.env` contain database passwords, image versions, container prefixes, and volume names.
- `compose.yml` and `immich.json` hold the mounted runtime configuration copied from source.
- `v31-fixtures.sql` and `v32-fixtures.sql` contain generated metadata updates for the four known fixture assets.

Sign in as **development@immich-lounge.test** using `adminEmail` and `adminPassword` from the relevant account JSON. Use this administrator account for manual photos and experiments.

The separate **fixtures@immich-lounge.test** account holds four synthetic photos and two overlapping albums. Keep its photos, albums, credentials, and sharing unchanged. Its `apiKey` has the minimum companion read permissions; seeding uses the fixture login for writes. The setup waits for upload metadata extraction, then writes deterministic dates and locations to those known fixture IDs in the dedicated database.

To test the companion UI, configure a dedicated companion instance with one of these local server URLs and the fixture `apiKey`, or create a separate key for the development administrator when using manual photos. Do not replace production connection settings for this workflow.

Keep credentials local. Never commit or publish `.local/immich/`. The generated account guide is refreshed by the lifecycle script; keep personal notes separately.

## Migration from `_out/live-immich/`

When `.local/immich/` does not exist, the script moves the primary checkout's former `_out/live-immich/` directory there. `Start` updates the configuration bind mount and recreates containers against the same external volumes. Accounts and fixture IDs remain unchanged. If both directories already exist, the script uses `.local/immich/`; reconcile any old copy manually before removing it.

## Containers and persistence

Development containers are `immich-lounge-dev-immich`, `immich-lounge-dev-postgres`, and `immich-lounge-dev-redis`. Legacy containers use the prefix `immich-lounge-compat-v31` with the same suffixes.

Library and database data live in separate named external Docker volumes. `IMMICH_VOLUME_PREFIX` preserves existing volume names, including the original `immich-lounge-search-v31/v32_library/database` names. New setups use the Docker project name as their volume prefix. Container/project renames and worktree removal do not delete data.

Keep `.local/immich/` alongside those volumes: the files hold the credentials and IDs needed to reuse the database. Stopping containers preserves both. Never delete these volumes as routine build-output cleanup, point them at a production library, or downgrade a database across major versions.

There is no automatic reset command. A deliberate reset must remove a stack's generated state and its matching volumes together before reseeding.