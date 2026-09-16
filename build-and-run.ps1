$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$ProgressPreference = 'SilentlyContinue'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$DotnetDir = Join-Path $Root '.dotnet'
$Dotnet = Join-Path $DotnetDir 'dotnet.exe'
$BootstrapDir = Join-Path $Root '.bootstrap'
$InstallScript = Join-Path $BootstrapDir 'dotnet-install.ps1'
$Project = Join-Path $Root 'src\KukaManager\KukaManager.csproj'
$SmokeProject = Join-Path $Root 'tests\KukaManager.SmokeTests\KukaManager.SmokeTests.csproj'
$PublishDir = Join-Path $Root 'dist\KukaManager'
$LogFile = Join-Path $Root 'build.log'
$NugetDir = Join-Path $Root '.nuget\packages'

function Log([string]$Text) {
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$stamp] $Text"
    Write-Host $line
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
}

function Run-Dotnet([string[]]$Args) {
    Log ('dotnet ' + ($Args -join ' '))
    & $Dotnet @Args 2>&1 | Tee-Object -FilePath $LogFile -Append
    if ($LASTEXITCODE -ne 0) { throw "dotnet 命令失败，退出代码 $LASTEXITCODE。请把 build.log 发给我。" }
}

try {
    Set-Content -Path $LogFile -Value '' -Encoding UTF8
    Log '酷咔 C#/.NET 10 首次构建开始'

    # Protect the existing Python-era database before the new executable opens it.
    $DataDir = Join-Path $env:LOCALAPPDATA 'KukaManager'
    $Db = Join-Path $DataDir 'kuka.sqlite3'
    if (Test-Path $Db) {
        $BackupDir = Join-Path $DataDir 'backups'
        New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $Pre = Join-Path $BackupDir "pre-csharp-$stamp.sqlite3"
        Copy-Item $Db $Pre -Force
        foreach ($suffix in @('-wal','-shm')) {
            $side = $Db + $suffix
            if (Test-Path $side) { Copy-Item $side ($Pre + $suffix) -Force }
        }
        Log "已备份现有数据库：$Pre"
    }

    New-Item -ItemType Directory -Force -Path $DotnetDir,$BootstrapDir,$NugetDir | Out-Null
    if (-not (Test-Path $Dotnet)) {
        Log '未检测到随包 .NET SDK，开始从微软官方下载 .NET 10.0.401 SDK。仅首次构建需要联网。'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $InstallScript
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $InstallScript -Version '10.0.401' -Architecture 'x64' -InstallDir $DotnetDir
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Dotnet)) { throw '下载/安装 .NET 10 SDK 失败。' }
    }

    $env:DOTNET_ROOT = $DotnetDir
    $env:PATH = "$DotnetDir;$env:PATH"
    $env:NUGET_PACKAGES = $NugetDir
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'

    Log '恢复 NuGet 依赖'
    Run-Dotnet @('restore',$Project,'--source','https://api.nuget.org/v3/index.json')
    Run-Dotnet @('restore',$SmokeProject,'--source','https://api.nuget.org/v3/index.json')

    Log '编译主程序与迁移冒烟测试'
    Run-Dotnet @('build',$SmokeProject,'-c','Release','--no-restore')

    Log '运行数据库/业务迁移冒烟测试'
    Run-Dotnet @('run','--project',$SmokeProject,'-c','Release','--no-build')

    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    Log '发布 Windows x64 Self-contained 正式可执行文件'
    Run-Dotnet @('publish',$Project,'-c','Release','-r','win-x64','--self-contained','true','--no-restore','-p:PublishReadyToRun=false','-p:DebugType=None','-p:DebugSymbols=false','-o',$PublishDir)

    $Exe = Join-Path $PublishDir 'KukaManager.exe'
    if (-not (Test-Path $Exe)) { throw '发布完成但没有找到 KukaManager.exe。' }
    $size = [math]::Round((Get-Item $Exe).Length / 1MB, 2)
    Log "构建成功：$Exe（主 EXE $size MB；其余运行库位于同目录）"

    $launcher = Join-Path $Root '启动酷咔.cmd'
    @"
@echo off
start "" "%~dp0dist\KukaManager\KukaManager.exe"
"@ | Set-Content -Path $launcher -Encoding ASCII

    Start-Process -FilePath $Exe -WorkingDirectory $PublishDir
    Log '酷咔已启动。以后直接双击“启动酷咔.cmd”或 dist\KukaManager\KukaManager.exe。'
}
catch {
    Log ('失败：' + $_.Exception.Message)
    Write-Host ''
    Write-Host '构建没有通过，所以没有启动未验证的程序。' -ForegroundColor Red
    Write-Host "日志：$LogFile" -ForegroundColor Yellow
    exit 1
}
