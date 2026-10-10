[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop', 'Status')]
    [string] $Action = 'Start',
    [ValidateSet('Both', 'Development', 'Legacy')]
    [string] $Target = 'Both'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
# Keep credentials and the mounted configuration in the primary checkout, surviving worktree removal.
$commonGitDir = (& git -C $root rev-parse --path-format=absolute --git-common-dir).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate the primary checkout for persistent test state.' }
$primaryRoot = Split-Path -Parent $commonGitDir
$output = Join-Path $primaryRoot '.local/immich'
$compose = Join-Path $output 'compose.yml'
$project = Join-Path $PSScriptRoot 'LiveImmich.csproj'
$stacks = @(
    @{ Name = 'v31'; Project = 'immich-lounge-compat-v31'; ContainerPrefix = 'immich-lounge-compat-v31'; Version = 'v3.1.0'; Port = 12283; Restart = 'no' },
    @{ Name = 'v32'; Project = 'immich-lounge-dev-immich'; ContainerPrefix = 'immich-lounge-dev'; Version = 'v3.2.0'; Port = 13283; Restart = 'unless-stopped' }
) | Where-Object { $Target -eq 'Both' -or ($Target -eq 'Development' -and $_.Name -eq 'v32') -or ($Target -eq 'Legacy' -and $_.Name -eq 'v31') }

docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop, then rerun this script.' }
# Migrate the former output location without changing volume names or account IDs.
$legacyOutput = [IO.Path]::GetFullPath((Join-Path $primaryRoot '_out/live-immich'))
if (!(Test-Path -LiteralPath $output) -and (Test-Path -LiteralPath $legacyOutput)) {
    $expectedSource = [IO.Path]::GetFullPath((Join-Path $primaryRoot '_out/live-immich'))
    $expectedTarget = [IO.Path]::GetFullPath((Join-Path $primaryRoot '.local/immich'))
    if ($legacyOutput -ne $expectedSource -or [IO.Path]::GetFullPath($output) -ne $expectedTarget) {
        throw 'Unexpected local state migration paths.'
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    Move-Item -LiteralPath $legacyOutput -Destination $output
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
# Also ignore state while this setup branch has not yet merged into the primary checkout.
[IO.File]::WriteAllText((Join-Path $output '.gitignore'), "*`n")
if ($Action -eq 'Start') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'compose.yml') -Destination $compose -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'immich.json') -Destination (Join-Path $output 'immich.json') -Force
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Development fixture seeder build failed.' }
}
$accountGuide = @"
# Local Immich development accounts

Generated local guide. Account passwords and API keys stay in the JSON files below; this guide does not duplicate them.

| Instance | URL | Docker project | Account file |
|---|---|---|---|
| Development (3.2) | http://127.0.0.1:13283 | immich-lounge-dev-immich | [v32-account.json](v32-account.json) |
| Legacy compatibility (3.1) | http://127.0.0.1:12283 | immich-lounge-compat-v31 | [v31-account.json](v31-account.json) |

## Which account to use

- **Manual development:** sign in as development@immich-lounge.test. Read adminEmail and adminPassword from the instance's account JSON. This is the administrator account; use it for manual photos, settings, and development experiments.
- **Automated fixtures:** fixtures@immich-lounge.test. Its login is stored as email and password; its restricted companion key is apiKey. Keep this account's photos, albums, credentials, and sharing unchanged.
- **Database:** DB_PASSWORD is stored in the instance's .env file. The database is internal to Docker and has no host port.

## Container names

Development: immich-lounge-dev-immich, immich-lounge-dev-postgres, immich-lounge-dev-redis.

Legacy: immich-lounge-compat-v31-immich, immich-lounge-compat-v31-postgres, immich-lounge-compat-v31-redis.

## Persistence

This folder is in the primary checkout and is gitignored. Docker volumes hold the photos and database; JSON/.env files hold the credentials needed to reuse them. Keep both. Existing volumes retain their original immich-lounge-search-v31/v32 names; renaming containers does not migrate or delete data.

Do not publish this directory or commit credentials. Start regenerates this guide; keep personal notes in a separate local file.
"@
[IO.File]::WriteAllText((Join-Path $output 'ACCOUNTS.local.md'), $accountGuide)
foreach ($stack in $stacks) {
    $envFile = Join-Path $output "$($stack.Name).env"
    if (!(Test-Path -LiteralPath $envFile)) {
        if ($Action -in 'Stop', 'Status') { Write-Host "$($stack.Name): not configured"; continue }
        $password = [Guid]::NewGuid().ToString('N')
        $configPath = (Join-Path $output 'immich.json').Replace('\', '/')
        [IO.File]::WriteAllText($envFile, "IMMICH_VERSION=$($stack.Version)`nIMMICH_PORT=$($stack.Port)`nIMMICH_RESTART=$($stack.Restart)`nDB_PASSWORD=$password`nIMMICH_CONFIG_PATH=$configPath`nIMMICH_STACK_NAME=$($stack.ContainerPrefix)`nIMMICH_VOLUME_PREFIX=$($stack.Project)`n")
    }
    # Repoint the bind mount after a state-directory migration; preserve all credentials.
    if ($Action -eq 'Start') {
        $configPath = (Join-Path $output 'immich.json').Replace('\', '/')
        $environment = [IO.File]::ReadAllText($envFile)
        $environment = [regex]::Replace($environment, '(?m)^IMMICH_CONFIG_PATH=.*$', "IMMICH_CONFIG_PATH=$configPath")
        [IO.File]::WriteAllText($envFile, $environment)
    }
    $composeArgs = @('compose', '-p', $stack.Project, '--env-file', $envFile, '-f', $compose)
    if ($Action -eq 'Stop') {
        & docker @composeArgs stop
        if ($LASTEXITCODE -ne 0) { throw "Could not stop $($stack.Name)." }
        continue
    }
    if ($Action -eq 'Status') {
        & docker @composeArgs ps
        continue
    }
    # Explicit external volume names keep data stable even if container/project names change.
    $environment = [IO.File]::ReadAllText($envFile)
    $prefixMatch = [regex]::Match($environment, '(?m)^IMMICH_VOLUME_PREFIX=([a-z0-9_-]+)\r?$')
    if (!$prefixMatch.Success) { throw "Missing or invalid IMMICH_VOLUME_PREFIX in $envFile." }
    foreach ($suffix in 'library', 'database') {
        $volumeName = $prefixMatch.Groups[1].Value + '_' + $suffix
        & docker volume create --label "immich-lounge.development=$($stack.Project)" $volumeName | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not prepare $volumeName." }
    }
    & docker @composeArgs up -d
    if ($LASTEXITCODE -ne 0) { throw "Could not start $($stack.Name)." }
    $url = "http://127.0.0.1:$($stack.Port)"
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    while ($true) {
        try {
            $version = Invoke-RestMethod -Uri "$url/api/server/version" -TimeoutSec 3
            if ($version.major -eq 3 -and $version.minor -eq ([int]$stack.Version.Split('.')[1])) { break }
        } catch { }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Timed out waiting for $($stack.Name). Inspect docker compose logs." }
        Start-Sleep -Seconds 2
    }
    # Fixed project names, ports, and new volumes: no real-library connection settings are accepted.
    dotnet run --project $project --configuration Release --no-build -- seed $stack.Name $output
    if ($LASTEXITCODE -ne 0) { throw "Could not seed $($stack.Name)." }
    $sqlPath = Join-Path $output "$($stack.Name)-fixtures.sql"
    Get-Content -LiteralPath $sqlPath -Raw -Encoding utf8 |
        & docker @composeArgs exec -T database psql -U postgres -d immich -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Fixture metadata setup failed for $($stack.Name)." }
    Write-Host "$($stack.Name) available at $url; account details: $output/$($stack.Name)-account.json"
}