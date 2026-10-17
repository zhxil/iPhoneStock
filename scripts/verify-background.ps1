param([switch]$Resume)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:IPHONE_STOCK_DATA = Join-Path $root 'artifacts/final-ui-data'
$exe = Join-Path $root 'dist/青岛iPhone库存助手/IphoneStock.exe'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type 'using System; using System.Runtime.InteropServices; public static class BackgroundWin { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); }'
function Find($window,[string]$name) { $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name)) }
function Click($window,[string]$name) { $e = Find $window $name; if (!$e) { throw "找不到控件 $name" }; $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
$app = if ($Resume) { Get-Process IphoneStock | Select-Object -First 1 } else { Start-Process -FilePath $exe -PassThru -WindowStyle Hidden }
Start-Sleep -Seconds 2
$app.Refresh(); $handle = $app.MainWindowHandle
if (!$handle) { throw '最终运行包启动失败' }
$window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
if (!$Resume) {
Click $window '开始监控'
[BackgroundWin]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2
if ([BackgroundWin]::IsWindowVisible($handle)) { throw '监控中关窗未隐藏' }
Write-Output "Final app PID $($app.Id), monitoring hidden. Waiting for second polling round."
for ($i=0;$i -lt 7;$i++) { Start-Sleep -Seconds 10; if ($app.HasExited) { throw '后台进程意外退出' } }
}
$duplicate = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
if (!$duplicate.HasExited -or ![BackgroundWin]::IsWindowVisible($handle)) { throw '单实例恢复失败' }
$logHeader = Find $window '运行记录'
try { $logHeader.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { }
$elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
$text = $elements | ForEach-Object { if ($_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit) { try { $_.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch {} } }
$log = $text -join "`n"
$log | Set-Content (Join-Path $root 'artifacts/background-log.txt')
$roundCount = [regex]::Matches($log, 'iPhone Duo 512GB 星光白色：').Count
if ($roundCount -lt 2) { throw "后台不足两轮：$log" }
Click $window '暂停监控'; Start-Sleep -Seconds 1
Click $window '退出程序'; Start-Sleep -Seconds 1
if (!$app.HasExited) { throw '正常退出失败' }
$saved = Get-Content (Join-Path $env:IPHONE_STOCK_DATA 'settings.v1.json') -Raw | ConvertFrom-Json
$saved.IntervalSeconds = 120
$saved | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $env:IPHONE_STOCK_DATA 'settings.v1.json')
$restart = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
$restart.Refresh(); $window = [System.Windows.Automation.AutomationElement]::FromHandle($restart.MainWindowHandle)
$header = Find $window '提醒与查询设置'
$header.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
$elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
$values = $elements | ForEach-Object { try { $_.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch {} }
if ('120' -notin $values) { throw '重启没有恢复已保存的120秒间隔' }
Click $window '测试提醒'; Start-Sleep -Seconds 1
if (!(Find $window '已提交测试提醒')) { throw '测试提醒按钮未完成' }
Click $window '退出程序'; Start-Sleep -Seconds 1
@{ timestamp = (Get-Date -Format o); backgroundRounds = $roundCount; hiddenWhileMonitoring = $true; duplicateRestores = $true; restartRestoresInterval = 120; testNotificationSubmitted = $true; cleanExit = $restart.HasExited; errors = @(Get-ChildItem $env:IPHONE_STOCK_DATA -Filter errors.log).Count } | ConvertTo-Json | Tee-Object -FilePath (Join-Path $root 'artifacts/final-lifecycle.json')
