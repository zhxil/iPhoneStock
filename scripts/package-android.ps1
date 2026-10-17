param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$stage=Join-Path $root ('.tools/android-source-v11-'+(Get-Date -Format yyyyMMdd-HHmmss))
$output=Join-Path $root 'dist/android'
New-Item -ItemType Directory -Force $stage,$output|Out-Null
Get-ChildItem (Join-Path $root 'android') -File -Recurse|Where-Object {$_.FullName -notmatch '[\\/](build|\.gradle)[\\/]' -and $_.Name -ne 'local.properties'}|ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($root,$_.FullName);$dest=Join-Path $stage $relative
 New-Item -ItemType Directory -Force (Split-Path -Parent $dest)|Out-Null
 Copy-Item -LiteralPath $_.FullName -Destination $dest
}
New-Item -ItemType Directory -Force (Join-Path $stage 'scripts')|Out-Null
Get-ChildItem (Join-Path $root 'scripts') -File|Where-Object {$_.Name -match '^(build-android|check-android|package-android)\.ps1$|^verify-android-.*\.py$'}|Copy-Item -Destination (Join-Path $stage 'scripts')
Copy-Item (Join-Path $root 'LICENSE'),(Join-Path $root 'NOTICE') -Destination $stage
Copy-Item (Join-Path $root 'LICENSE'),(Join-Path $root 'NOTICE') -Destination $output
Copy-Item (Join-Path $root 'android/README.md') -Destination (Join-Path $output '使用说明.md')
Copy-Item (Join-Path $root 'android/SUPER_ISLAND.md') -Destination (Join-Path $output '超级岛接入说明.md')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $output 'Android-v1.1.0-对应源码.zip') -Force
$names=@('iPhone库存助手-Android-v1.1.0.apk','Android-v1.1.0-对应源码.zip','使用说明.md','超级岛接入说明.md','LICENSE','NOTICE','验证记录-v1.1.txt')
$names|ForEach-Object { $path=Join-Path $output $_; if(Test-Path $path){'{0}  {1}' -f (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant(),$_} }|Set-Content (Join-Path $output 'SHA256SUMS-v1.1.txt') -Encoding utf8
Get-FileHash (Join-Path $output 'iPhone库存助手-Android-v1.1.0.apk'),(Join-Path $output 'Android-v1.1.0-对应源码.zip') -Algorithm SHA256
