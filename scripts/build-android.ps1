param([string]$Sdk = 'D:/AndroidSDK', [string]$JavaHome = '', [string]$XiaomiAppId = '', [string]$Gradle = '')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Set-Location $root
if(!$JavaHome){$JavaHome='D:/Program Files/Java/jdk-25'}
if(!$Gradle){
 $candidate=Get-ChildItem (Join-Path $env:USERPROFILE '.gradle/wrapper/dists/.gradle-bootstrap') -Filter gradle.bat -Recurse -ErrorAction SilentlyContinue|Select-Object -First 1
 if($candidate){$Gradle=$candidate.FullName}else{$Gradle='gradle'}
}
$versionMatch=[regex]::Match((Get-Content (Join-Path $root 'android/app/build.gradle') -Raw), "versionName '([^']+)'")
if(!$versionMatch.Success){throw '无法读取 Android 版本号'}
$version=$versionMatch.Groups[1].Value
$env:JAVA_HOME=$JavaHome;$env:ANDROID_HOME=$Sdk
$build=Join-Path $env:TEMP ('iphone-stock-compose-'+(Get-Date -Format yyyyMMdd-HHmmss))
$project=Join-Path $build 'project'
$signing=Join-Path $root '.tools/android-signing';$output=Join-Path $root 'dist/android'
New-Item -ItemType Directory -Force $project,$signing,$output|Out-Null
Copy-Item android/settings.gradle,android/build.gradle,android/gradle.properties -Destination $project
New-Item -ItemType Directory -Force (Join-Path $project 'app')|Out-Null
Copy-Item android/app/build.gradle -Destination (Join-Path $project 'app')
Copy-Item android/app/src -Destination (Join-Path $project 'app') -Recurse
[IO.File]::WriteAllText((Join-Path $project 'local.properties'),'sdk.dir='+$Sdk.Replace('\','/'))
function EnsureExit([string]$step){if($LASTEXITCODE -ne 0){throw "$step 失败，退出码 $LASTEXITCODE"}}
$gradleArgs=@('-p',$project,'--no-daemon','--console=plain','-PxiaomiAppId='+$XiaomiAppId)
if($env:HTTPS_PROXY){$proxy=[Uri]$env:HTTPS_PROXY;$gradleArgs+=@("-Dhttps.proxyHost=$($proxy.Host)","-Dhttps.proxyPort=$($proxy.Port)","-Dhttp.proxyHost=$($proxy.Host)","-Dhttp.proxyPort=$($proxy.Port)")}
& $Gradle @gradleArgs ':app:assembleRelease';EnsureExit 'Compose APK 构建'
$tools=Join-Path $Sdk 'build-tools/36.0.0'
$aligned=Join-Path $build 'aligned.apk'
& (Join-Path $tools 'zipalign.exe') -f -p 4 (Join-Path $project 'app/build/outputs/apk/release/app-release-unsigned.apk') $aligned;EnsureExit 'APK 对齐'
$keystore = Join-Path $signing 'release.p12'; $passwordFile = Join-Path $signing 'password.txt'
if (!(Test-Path $keystore)) {
    if (Test-Path $passwordFile) { throw '签名密码存在但密钥缺失，请先检查签名目录' }
    $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    [IO.File]::WriteAllText($passwordFile,$password,[Text.Encoding]::ASCII)
    & (Join-Path $JavaHome 'bin/keytool.exe') -genkeypair -keystore $keystore -storetype PKCS12 -storepass:file $passwordFile -keypass:file $passwordFile -alias iphone-stock -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Qingdao iPhone Stock Local,OU=Personal Tools,O=Local,C=CN'; EnsureExit '创建签名密钥'
}
$apk = Join-Path $build "IphoneStock-v$version.apk"
& (Join-Path $JavaHome 'bin/java.exe') -jar (Join-Path $tools 'lib/apksigner.jar') sign --ks $keystore --ks-key-alias iphone-stock --ks-pass "file:$passwordFile" --out $apk $aligned; EnsureExit 'APK 签名'
& (Join-Path $JavaHome 'bin/java.exe') -jar (Join-Path $tools 'lib/apksigner.jar') verify --verbose --print-certs $apk | Tee-Object -FilePath (Join-Path $root 'artifacts/android-signature.txt'); EnsureExit '签名检查'
& (Join-Path $tools 'aapt2.exe') dump badging $apk | Set-Content (Join-Path $root 'artifacts/android-badging.txt'); EnsureExit 'APK 清单检查'
$destination = Join-Path $output "iPhone库存助手-Android-v$version.apk"
Copy-Item -LiteralPath $apk -Destination $destination
Get-FileHash -LiteralPath $destination -Algorithm SHA256 | ConvertTo-Json | Tee-Object -FilePath (Join-Path $root 'artifacts/android-hash.json')
Write-Output "APK: $destination"
