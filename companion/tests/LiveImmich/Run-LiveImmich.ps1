[CmdletBinding()]
param(
    [ValidateSet('Start', 'Test', 'Stop', 'Status')]
    [string] $Action = 'Test',
    [ValidateSet('Both', 'Development', 'Legacy')]
    [string] $Target = 'Both'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
# Keep credentials and the mounted configuration in the primary checkout, surviving worktree removal.
$commonGitDir = (& git -C $root rev-parse --path-format=absolute --git-common-dir).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate the primary checkout for persistent test state.' }
$primaryRoot = Split-Path -Parent $commonGitDir
$output = Join-Path $primaryRoot '_out/live-immich'
$compose = Join-Path $output 'compose.yml'
$project = Join-Path $PSScriptRoot 'LiveImmich.csproj'
$stacks = @(
    @{ Name = 'v31'; Version = 'v3.1.0'; Port = 12283; Restart = 'no' },
    @{ Name = 'v32'; Version = 'v3.2.0'; Port = 13283; Restart = 'unless-stopped' }
) | Where-Object { $Target -eq 'Both' -or ($Target -eq 'Development' -and $_.Name -eq 'v32') -or ($Target -eq 'Legacy' -and $_.Name -eq 'v31') }

docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop, then rerun this script.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
if ($Action -in 'Start', 'Test') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'compose.yml') -Destination $compose -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'immich.json') -Destination (Join-Path $output 'immich.json') -Force
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Live test runner build failed.' }
}
foreach ($stack in $stacks) {
    $envFile = Join-Path $output "$($stack.Name).env"
    if (!(Test-Path -LiteralPath $envFile)) {
        if ($Action -in 'Stop', 'Status') { Write-Host "$($stack.Name): not configured"; continue }
        $password = [Guid]::NewGuid().ToString('N')
        $configPath = (Join-Path $output 'immich.json').Replace('\', '/')
        [IO.File]::WriteAllText($envFile, "IMMICH_VERSION=$($stack.Version)`nIMMICH_PORT=$($stack.Port)`nIMMICH_RESTART=$($stack.Restart)`nDB_PASSWORD=$password`nIMMICH_CONFIG_PATH=$configPath`n")
    }
    $composeArgs = @('compose', '-p', "immich-lounge-search-$($stack.Name)", '--env-file', $envFile, '-f', $compose)
    if ($Action -eq 'Stop') {
        & docker @composeArgs stop
        if ($LASTEXITCODE -ne 0) { throw "Could not stop $($stack.Name)." }
        continue
    }
    if ($Action -eq 'Status') {
        & docker @composeArgs ps
        continue
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
    if ($Action -eq 'Test') {
        dotnet run --project $project --configuration Release --no-build -- test $stack.Name $output
        if ($LASTEXITCODE -ne 0) { throw "Live checks failed for $($stack.Name)." }
    }
    Write-Host "$($stack.Name) available at $url; account details: $output/$($stack.Name)-account.json"
}