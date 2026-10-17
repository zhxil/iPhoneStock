$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Remove-Item Env:IPHONE_STOCK_DATA -ErrorAction SilentlyContinue
Remove-Item Env:IPHONE_STOCK_TRACE -ErrorAction SilentlyContinue
$exe=Join-Path $root 'dist/青岛iPhone库存助手/IphoneStock.exe'
$runKey='HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
$old=(Get-ItemProperty $runKey -Name QingdaoIphoneStock -ErrorAction SilentlyContinue).QingdaoIphoneStock
@{before=$old;timestamp=(Get-Date -Format o)}|ConvertTo-Json|Set-Content (Join-Path $root 'artifacts/startup-before.json')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$app=Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2;$app.Refresh();$main=$app.MainWindowHandle
$w=[System.Windows.Automation.AutomationElement]::FromHandle($main)
function Find([string]$name){$w.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))}
function Click([string]$name){$e=Find $name;if(!$e){throw "控件不存在：$name"};$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
$header=Find '提醒与查询设置';$header.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
$toggle=Find '开机启动并开始监控 关闭'
if($toggle){$toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()}
Click '保存设置';Start-Sleep -Milliseconds 500
$value=(Get-ItemProperty $runKey -Name QingdaoIphoneStock).QingdaoIphoneStock
if($value -ne ('"'+$exe+'" --startup')){throw "启动命令不正确：$value"}
Click '退出程序';Start-Sleep -Seconds 1
if(!$app.HasExited){throw '启动入口测试前退出失败'}
$boot=Start-Process -FilePath $exe -ArgumentList '--startup' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 4;$boot.Refresh()
if($boot.HasExited){throw '启动入口进程意外退出'}
$data=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'QingdaoIphoneStock/settings.v1.json'
$settings=Get-Content $data -Raw|ConvertFrom-Json
if(!$settings.StartupEnabled){throw '开机启动未保存'}
@{timestamp=(Get-Date -Format o);enabled=$true;scope='HKCU Run';command=$value;processId=$boot.Id;mainWindowHandle=$boot.MainWindowHandle.ToInt64();startupEntryInvoked=$true;settingsPath=$data;settings=$settings}|ConvertTo-Json -Depth 6|Tee-Object -FilePath (Join-Path $root 'artifacts/startup-verification.json')
