param(
    [string]$InputPath = "_out/build/channel",
    [string]$Severity = "warning",
    [string]$ExitOn = "error",
    [string]$OutputPath = "",
    [string]$Format = "console"
)

$ErrorActionPreference = 'Stop'
$env:HTTP_PROXY = $null
$env:HTTPS_PROXY = $null
$env:http_proxy = $null
$env:https_proxy = $null

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$toolRoot = Join-Path $repoRoot ".cache\roku-sca\sca-cmd"
$jarPath = Join-Path $toolRoot "lib\sca-cmd.jar"

if (-not (Test-Path $jarPath)) {
    $archivePath = Join-Path $toolRoot "..\sca-cmd.zip"
    New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
    Invoke-WebRequest -Uri "https://devtools.web.roku.com/static-channel-analysis/sca-cmd.zip" -OutFile $archivePath
    Expand-Archive -Path $archivePath -DestinationPath (Split-Path $toolRoot -Parent) -Force
}

$resolvedInput = Resolve-Path $InputPath
$args = @(
    "-jar",
    $jarPath,
    $resolvedInput.Path,
    "--severity",
    $Severity,
    "--exit",
    $ExitOn
)

if ($OutputPath -ne "") {
    $args += @("--output", $OutputPath, "--format", $Format)
}

& java @args
exit $LASTEXITCODE
