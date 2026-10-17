param([int]$ProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiNative {
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
'@
[UiNative]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$root = Split-Path -Parent $PSScriptRoot
$process = Get-Process -Id $ProcessId
$windowHandle = $process.MainWindowHandle
$window = [System.Windows.Automation.AutomationElement]::FromHandle($windowHandle)
$scope = [System.Windows.Automation.TreeScope]::Descendants
function Find([string]$name) {
 $window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
}
function Invoke([string]$name) {
 $element = Find $name
 if (!$element) { throw "控件缺失：$name" }
 $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
 $pattern.Invoke()
}
$checks = [System.Collections.Generic.List[string]]::new()
if (!(Find 'iPhone Duo 512GB 星光白色')) { throw '预设商品未显示' }; $checks.Add('preset-visible')
Invoke '查询一次'
for ($i=0;$i -lt 25;$i++) { Start-Sleep -Seconds 1; if (Find '单次查询完成') { break } }
if (!(Find '单次查询完成')) { throw '单次查询未结束' }; $checks.Add('single-query-completes')
$names = $window.FindAll($scope,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object {$_.Current.Name}
$names | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts/ui-after-query.json')
Invoke '开始监控'; Start-Sleep -Seconds 1
if (!(Find '暂停监控')) { throw '无法启动持续监控' }; $checks.Add('monitor-starts')
Invoke '暂停监控'; Start-Sleep -Seconds 1
if (!(Find '已暂停')) { throw '无法暂停' }; $checks.Add('monitor-pauses')
[UiNative]::PostMessage($windowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 1
if ([UiNative]::IsWindowVisible($windowHandle)) { throw '关窗未隐藏' }
if (!(Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { throw '关窗导致进程退出' }; $checks.Add('close-hides-process-keeps-running')
$env:IPHONE_STOCK_DATA = Join-Path $root 'artifacts/ui-smoke-data'
$duplicate = Start-Process -FilePath (Join-Path $root 'dist/青岛iPhone库存助手/IphoneStock.exe') -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
if (!$duplicate.HasExited) { throw '重复进程没有退出' }
if (![UiNative]::IsWindowVisible($windowHandle)) { throw '重复启动未恢复窗口' }; $checks.Add('single-instance-restores-window')
$checks | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts/ui-checks.json')
$checks
