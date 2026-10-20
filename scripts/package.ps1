param([string]$ReleaseDirectory = 'dist/青岛iPhone库存助手', [string]$ArchivePath = 'dist/iPhone库存助手-Windows-x64.zip')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root $ReleaseDirectory
$stage = Join-Path $root ('.tools/source-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $stage | Out-Null
foreach ($folder in @('native','scripts','docs')) {
    $base = Join-Path $root $folder
    Get-ChildItem $base -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
        $destination = Join-Path $stage $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
}
Copy-Item (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE'),(Join-Path $root 'NOTICE'),(Join-Path $root '使用说明.txt') -Destination $stage
Copy-Item (Join-Path $root 'LICENSE'),(Join-Path $root 'NOTICE'),(Join-Path $root '使用说明.txt') -Destination $release
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $release '对应源码.zip') -Force
$files = Get-ChildItem $release -File -Recurse | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetRelativePath($release,$_.FullName)
}
$files | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding utf8
Compress-Archive -LiteralPath $release -DestinationPath (Join-Path $root $ArchivePath) -Force
Get-FileHash (Join-Path $root $ArchivePath) -Algorithm SHA256
