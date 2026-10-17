param([string]$JavaHome = 'D:/Program Files/Java/jdk-25', [switch]$Live)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Set-Location $root
$cache=Join-Path $root '.tools/android-tests'
New-Item -ItemType Directory -Force $cache | Out-Null
if (!(Test-Path (Join-Path $cache 'json-20240303.jar'))) {
    node -e "fetch('https://repo.maven.apache.org/maven2/org/json/json/20240303/json-20240303.jar').then(async r=>{if(!r.ok)throw Error(r.status);require('fs').writeFileSync('.tools/android-tests/json-20240303.jar',Buffer.from(await r.arrayBuffer()));}).catch(e=>{process.stderr.write(String(e));process.exitCode=1})"
    if($LASTEXITCODE -ne 0){throw '无法下载桌面 JVM 测试依赖'}
}
$work=Join-Path ([IO.Path]::GetTempPath()) ('iphone-stock-java-checks-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $work | Out-Null
Copy-Item -LiteralPath (Join-Path $cache 'json-20240303.jar') -Destination (Join-Path $work 'json.jar')
Copy-Item android/app/src/main/java/local/qingdao/iphonestock/Stock.java,android/app/src/main/java/local/qingdao/iphonestock/AppleClient.java,android/app/src/main/java/local/qingdao/iphonestock/FocusPayload.java,android/tests/StockChecks.java,android/tests/FocusChecks.java -Destination $work
& (Join-Path $JavaHome 'bin/javac.exe') --release 17 -encoding UTF-8 -classpath (Join-Path $work 'json.jar') -d (Join-Path $work 'classes') (Join-Path $work 'Stock.java') (Join-Path $work 'AppleClient.java') (Join-Path $work 'FocusPayload.java') (Join-Path $work 'StockChecks.java') (Join-Path $work 'FocusChecks.java')
if($LASTEXITCODE -ne 0){throw '协议测试编译失败'}
$argsList=@('-Dstdout.encoding=UTF-8','-Dstderr.encoding=UTF-8','-cp',"$(Join-Path $work 'classes');$(Join-Path $work 'json.jar')",'StockChecks')
if($Live){$argsList+='--live'}
& (Join-Path $JavaHome 'bin/java.exe') @argsList
if($LASTEXITCODE -ne 0){throw '协议检查失败'}
& (Join-Path $JavaHome 'bin/java.exe') '-Dstdout.encoding=UTF-8' '-Dstderr.encoding=UTF-8' '-cp' "$(Join-Path $work 'classes');$(Join-Path $work 'json.jar')" 'FocusChecks'
if($LASTEXITCODE -ne 0){throw '超级岛协议检查失败'}
