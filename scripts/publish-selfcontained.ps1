<#
.SYNOPSIS
    Builds the self-contained Windows x64 folder you upload to an IIS host, and proves it starts.

.DESCRIPTION
    Output goes to .\publish (gitignored). The folder is meant to be uploaded WHOLE and always works on its own:

      * The JWT signing key is written into the published appsettings.json, so there is no second file to forget
        to upload and no dependence on ASPNETCORE_ENVIRONMENT. Without a key the app refuses to start outside
        Development, which IIS reports as "HTTP Error 500.30 - ASP.NET Core app failed to start".
        The key is REUSED from the previous publish when there is one (a new key logs every user out) and is
        generated once otherwise. It never touches git: publish\ is ignored.
      * stdout logging is switched on (logs\), so a startup failure leaves a readable reason on the server.
        Turn it off in web.config (stdoutLogEnabled="false") once the site is healthy.
      * Entity Framework's per-query logging is turned down so those logs stay small.
      * The development settings file and debug symbols are left out.
      * Finally the folder is copied and started the way IIS would start it (no overrides, run from its own
        directory), and the script fails if /api/version does not answer.

    The database is NOT part of this script. To ship one, copy a SQLite snapshot to publish\intellimed.db
    afterwards (first deploy only - re-uploading it replaces the live data).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\publish-selfcontained.ps1
#>
param(
    [string]$Output = 'publish',
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root $Output
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Key([string]$folder) {
    foreach ($name in 'appsettings.json', 'appsettings.Production.json') {
        $file = Join-Path $folder $name
        if (Test-Path $file) {
            try {
                $key = (Get-Content $file -Raw | ConvertFrom-Json).Jwt.Key
                if ($key -and $key.Length -ge 43) { return $key }   # >= 32 bytes once decoded
            } catch { }
        }
    }
    return $null
}

# Keep the existing key so a rebuild does not sign everyone out.
$key = Read-Key $out
$reused = [bool]$key
if (-not $key) {
    $bytes = New-Object byte[] 64
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $key = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

& dotnet publish (Join-Path $root 'src\IntelliMed.Api\IntelliMed.Api.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

Remove-Item (Join-Path $out 'appsettings.Development.json') -ErrorAction SilentlyContinue
Remove-Item (Join-Path $out 'appsettings.Production.json') -ErrorAction SilentlyContinue

# --- appsettings.json: key in, EF query logging down -------------------------------------------------------
$settingsPath = Join-Path $out 'appsettings.json'
$cfg = Get-Content $settingsPath -Raw | ConvertFrom-Json
if (-not $cfg.Jwt) { $cfg | Add-Member -NotePropertyName Jwt -NotePropertyValue ([pscustomobject]@{}) }
$cfg.Jwt | Add-Member -NotePropertyName Key -NotePropertyValue $key -Force
$cfg.Logging.LogLevel | Add-Member -NotePropertyName 'Microsoft.EntityFrameworkCore' -NotePropertyValue 'Warning' -Force
[System.IO.File]::WriteAllText($settingsPath, ($cfg | ConvertTo-Json -Depth 10), $utf8)

# --- web.config: keep a stdout log so a failed start explains itself ---------------------------------------
$webConfig = Join-Path $out 'web.config'
(Get-Content $webConfig -Raw) -replace 'stdoutLogEnabled="false"', 'stdoutLogEnabled="true"' |
    ForEach-Object { [System.IO.File]::WriteAllText($webConfig, $_, $utf8) }
New-Item -ItemType Directory -Path (Join-Path $out 'logs') -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $out 'logs\README.txt'),
    "IIS writes the app's startup/console output here (stdout_*.log). If the site shows HTTP 500.30, open the newest file.`r`n" +
    "Set stdoutLogEnabled=`"false`" in ..\web.config once the site is healthy.`r`n", $utf8)

$files = (Get-ChildItem $out -Recurse -File).Count
$mb = [math]::Round(((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB))
Write-Host "Published $files files, $mb MB to $out  (JWT key $(if ($reused) { 'reused' } else { 'NEW - generated' }))"

# --- prove it starts the way IIS will start it --------------------------------------------------------------
if ($SkipVerify) { return }

$check = Join-Path ([System.IO.Path]::GetTempPath()) ('intellimed-verify-' + [guid]::NewGuid().ToString('N'))
Copy-Item $out $check -Recurse
$port = Get-Random -Minimum 5400 -Maximum 5900
$psi = New-Object System.Diagnostics.ProcessStartInfo (Join-Path $check 'IntelliMed.Api.exe')
$psi.WorkingDirectory = $check
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
foreach ($v in @($psi.EnvironmentVariables.Keys)) { if ($v -like 'ASPNETCORE*') { $psi.EnvironmentVariables.Remove($v) } }
$psi.EnvironmentVariables['ASPNETCORE_URLS'] = "http://localhost:$port"   # only to avoid a port clash
$proc = [System.Diagnostics.Process]::Start($psi)
$status = $null
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline -and -not $proc.HasExited -and -not $status) {
    try { $status = (Invoke-WebRequest "http://localhost:$port/api/version" -UseBasicParsing -TimeoutSec 3).StatusCode } catch { Start-Sleep -Seconds 1 }
}
$exited = $proc.HasExited
$why = if ($exited) { $proc.StandardOutput.ReadToEnd() + $proc.StandardError.ReadToEnd() } else { '' }
if (-not $exited) { $proc.Kill() }
Start-Sleep -Milliseconds 500
Remove-Item $check -Recurse -Force -ErrorAction SilentlyContinue

if ($status -ne 200) {
    Write-Host $why
    throw "VERIFY FAILED: the published folder did not start (status '$status', exited: $exited)."
}
Write-Host "Verified: the folder starts cleanly from its own directory with no environment overrides (/api/version = 200)."
