param([string]$Dotnet = 'dotnet', [string]$FallbackPackages = '', [string]$OutputDirectory = 'dist/青岛iPhone库存助手')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:DOTNET_CLI_HOME = Join-Path $root '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $root '.tools/packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $root '.tools/http-cache'
$env:NUGET_SCRATCH = Join-Path $root '.tools/scratch'
$env:APPDATA = Join-Path $root '.tools/appdata'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Force $env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES,$env:NUGET_SCRATCH,$env:APPDATA | Out-Null
$restoreArgs = @()
if ($FallbackPackages) { $restoreArgs += "-p:RestoreFallbackFolders=$FallbackPackages" }
& $Dotnet run --project native/Stock.Checks -c Release @restoreArgs
if ($LASTEXITCODE -ne 0) { throw '核心检查失败' }
& $Dotnet publish native/Stock.WinUI -c Release -r win-x64 --self-contained true -o $OutputDirectory @restoreArgs
if ($LASTEXITCODE -ne 0) { throw 'WinUI 3 发布失败' }
Copy-Item LICENSE,NOTICE,使用说明.txt -Destination $OutputDirectory
Get-FileHash (Join-Path $OutputDirectory 'IphoneStock.exe') -Algorithm SHA256 | Format-List
