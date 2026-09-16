$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ProgressPreference = 'SilentlyContinue'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogFile = Join-Path $Root 'build.log'
$Project = Join-Path $Root 'src\KukaManager\KukaManager.csproj'
$SmokeProject = Join-Path $Root 'tests\KukaManager.SmokeTests\KukaManager.SmokeTests.csproj'
$PublishDir = Join-Path $Root 'dist\KukaManager'
$LocalDotnetDir = Join-Path $Root '.dotnet'
$LocalDotnet = Join-Path $LocalDotnetDir 'dotnet.exe'
$SdkZip = Join-Path $Root '.dotnet-sdk.zip'
$DevToolsDir = Join-Path $env:LOCALAPPDATA 'KukaManager\devtools'
$SharedDotnetDir = Join-Path $DevToolsDir 'dotnet'
$SharedDotnet = Join-Path $SharedDotnetDir 'dotnet.exe'
$NugetDir = Join-Path $DevToolsDir 'nuget-packages'
$SdkUrl = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip'

function Log([string]$Text) {
    $line = ('[{0}] {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Text)
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
}
function Fail([string]$Text) { throw $Text }
function Find-Dotnet10 {
    $candidates = @()
    if (Test-Path $SharedDotnet) { $candidates += $SharedDotnet }
    if (Test-Path $LocalDotnet) { $candidates += $LocalDotnet }
    $pf = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $pf) { $candidates += $pf }

    # Reuse a private .NET 10 SDK downloaded by an earlier Kuka builder folder.
    # This avoids downloading ~200 MB again for every small builder fix.
    $parent = Split-Path -Parent $Root
    try {
        Get-ChildItem -LiteralPath $parent -Directory -Filter 'KukaManager-CSharp-*' -ErrorAction SilentlyContinue | ForEach-Object {
            $siblingDotnet = Join-Path $_.FullName '.dotnet\dotnet.exe'
            if (Test-Path $siblingDotnet) { $candidates += $siblingDotnet }
        }
    } catch {}

    try {
        $cmd = Get-Command dotnet.exe -ErrorAction Stop
        if ($cmd.Source) { $candidates += $cmd.Source }
    } catch {}
    foreach ($d in ($candidates | Select-Object -Unique)) {
        try {
            $v = (& $d --version 2>$null).Trim()
            if ($v -match '^10\.') { return $d }
        } catch {}
    }
    return $null
}
function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)][string]$DotnetPath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    $displayArgs = ($ArgumentList | ForEach-Object {
        if ($_ -match '\\s') { '"' + $_ + '"' } else { $_ }
    }) -join ' '
    Log ('dotnet ' + $displayArgs)

    # IMPORTANT: do not name this parameter $Args. $args is a PowerShell automatic
    # variable; using that name caused the v1.0.1/v1.0.2 builder to invoke dotnet
    # with an empty argument list.
    $nativeOutput = & $DotnetPath @ArgumentList 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in $nativeOutput) {
        $text = [string]$line
        Write-Host $text
        Add-Content -LiteralPath $LogFile -Value $text -Encoding UTF8
    }
    if ($exitCode -ne 0) { Fail "dotnet command failed with exit code $exitCode" }
}

try {
    Set-Content -LiteralPath $LogFile -Value '' -Encoding UTF8
    Log 'KukaManager C# builder v6 started.'
    Log ('Windows: ' + [Environment]::OSVersion.VersionString)
    Log ('PowerShell: ' + $PSVersionTable.PSVersion)

    if (-not (Test-Path $Project)) { Fail "Project not found: $Project" }
    if (-not (Test-Path $SmokeProject)) { Fail "Smoke test project not found: $SmokeProject" }

    # Production database is backed up only after compile/tests/publish succeed.

    $dotnet = Find-Dotnet10
    if (-not $dotnet) {
        Log 'No .NET 10 SDK found. Installing a shared private SDK for Kuka builders.'
        New-Item -ItemType Directory -Force -Path $SharedDotnetDir | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        try {
            Log "Downloading official .NET 10.0.401 SDK from Microsoft."
            Invoke-WebRequest -UseBasicParsing -Uri $SdkUrl -OutFile $SdkZip -TimeoutSec 900
        } catch {
            Log ('Direct SDK download failed: ' + $_.Exception.Message)
            $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
            if ($winget) {
                Log 'Trying winget fallback: Microsoft.DotNet.SDK.10'
                & $winget.Source install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-package-agreements --accept-source-agreements 2>&1 | Tee-Object -FilePath $LogFile -Append
                $dotnet = Find-Dotnet10
            }
            if (-not $dotnet) { Fail 'Could not obtain .NET 10 SDK. Check internet/Windows security and build.log.' }
        }
        if (-not $dotnet -and (Test-Path $SdkZip)) {
            Log 'Expanding .NET SDK.'
            Expand-Archive -LiteralPath $SdkZip -DestinationPath $SharedDotnetDir -Force
            Remove-Item -LiteralPath $SdkZip -Force -ErrorAction SilentlyContinue
            $dotnet = Find-Dotnet10
        }
    }
    if (-not $dotnet) { Fail '.NET 10 SDK is still unavailable.' }
    Log "Using SDK: $dotnet"
    Log ('SDK version: ' + (& $dotnet --version))

    $dotnetRoot = Split-Path -Parent $dotnet
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$env:PATH"
    $env:NUGET_PACKAGES = $NugetDir
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'
    New-Item -ItemType Directory -Force -Path $NugetDir | Out-Null

    Log 'Restoring dependencies.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('restore', $Project, '--source', 'https://api.nuget.org/v3/index.json')
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('restore', $SmokeProject, '--source', 'https://api.nuget.org/v3/index.json')

    Log 'Building main C# application.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('build', $Project, '-c', 'Release', '--no-restore')

    Log 'Building migration smoke tests.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('build', $SmokeProject, '-c', 'Release', '--no-restore')
    Log 'Running migration smoke tests.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('run', '--project', $SmokeProject, '-c', 'Release', '--no-build')

    Log 'Restoring Windows x64 publish assets.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('restore', $Project, '-r', 'win-x64', '--source', 'https://api.nuget.org/v3/index.json')

    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    Log 'Publishing Windows x64 self-contained app.'
    Invoke-Dotnet -DotnetPath $dotnet -ArgumentList @('publish', $Project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-p:PublishReadyToRun=false', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $PublishDir)
    $exe = Join-Path $PublishDir 'KukaManager.exe'
    if (-not (Test-Path $exe)) { Fail 'Publish finished but KukaManager.exe was not created.' }
    $exeInfo = Get-Item -LiteralPath $exe
    if ($exeInfo.Length -lt 100000) { Fail ('Published KukaManager.exe is unexpectedly small: ' + $exeInfo.Length + ' bytes') }

    # Only now touch the production data area: compilation and smoke tests already passed.
    $dataDir = Join-Path $env:LOCALAPPDATA 'KukaManager'
    $db = Join-Path $dataDir 'kuka.sqlite3'
    if (Test-Path $db) {
        $backupDir = Join-Path $dataDir 'backups'
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $pre = Join-Path $backupDir "pre-csharp-$stamp.sqlite3"
        Copy-Item -LiteralPath $db -Destination $pre -Force
        foreach ($suffix in @('-wal','-shm')) {
            $side = $db + $suffix
            if (Test-Path $side) { Copy-Item -LiteralPath $side -Destination ($pre + $suffix) -Force }
        }
        Log "Database backup created: $pre"
    }

    Log ('SUCCESS: ' + $exe)
    $process = Start-Process -FilePath $exe -WorkingDirectory $PublishDir -PassThru
    Start-Sleep -Seconds 3
    if ($process.HasExited) { Fail ('KukaManager.exe exited immediately with code ' + $process.ExitCode + '. Check %LOCALAPPDATA%\KukaManager\logs for startup details.') }
    Log ('Application launched successfully. PID=' + $process.Id)
    exit 0
} catch {
    Log ('FAILED: ' + $_.Exception.ToString())
    Write-Host ''
    Write-Host 'BUILD FAILED. The window will remain open.' -ForegroundColor Red
    Write-Host "Log: $LogFile" -ForegroundColor Yellow
    exit 1
}
