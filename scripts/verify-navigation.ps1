param([int]$ProcessId, [string]$EvidenceDirectory = 'artifacts/ui-v12', [switch]$Exercise)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $root $EvidenceDirectory
New-Item -ItemType Directory -Force $evidence | Out-Null
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;
public static class NavigationCheck {
 public delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]public static extern bool EnumChildWindows(IntPtr h,EnumProc proc,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 public struct Rect {public int Left,Top,Right,Bottom;}
}
'@
[NavigationCheck]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$process=Get-Process -Id $ProcessId
$handle=$process.MainWindowHandle
if(!$handle){throw '请先从任务栏展开主窗口后运行界面验证'}
[NavigationCheck]::ShowWindow($handle,9)|Out-Null
$window=[System.Windows.Automation.AutomationElement]::FromHandle($handle)
$scope=[System.Windows.Automation.TreeScope]::Descendants
function FindId([string]$id) { $window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)) }
function ClickId([string]$id) {
 $e=FindId $id;if(!$e){throw "控件不存在：$id"}
 $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Choose([System.Windows.Automation.AutomationElement]$combo,[string]$name) {
 $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $item=$combo.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
 if(!$item){throw "下拉选项不存在：$name"}
 $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 150
}
function Navigate([string]$id) {
 [NavigationCheck]::ShowWindow($handle,9)|Out-Null
 [NavigationCheck]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
 Start-Sleep -Milliseconds 150
 $e=FindId $id;if(!$e){throw "导航项不存在：$id"}
 $r=$e.Current.BoundingRectangle
 [NavigationCheck]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
 [NavigationCheck]::SetCursorPos($r.X+$r.Width/2,$r.Y+$r.Height/2)|Out-Null
 [NavigationCheck]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[NavigationCheck]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 500
 [NavigationCheck]::SetWindowPos($handle,[IntPtr](-2),0,0,0,0,0x13)|Out-Null
}
function Shot([string]$name) {
 [NavigationCheck]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
 Start-Sleep -Milliseconds 250
 try {
  $path=& C:/Users/zhang/.codex/skills/screenshot/scripts/take_screenshot.ps1 -Mode temp -WindowHandle $handle
  Copy-Item -LiteralPath $path -Destination (Join-Path $evidence "$name.png")
 }finally{[NavigationCheck]::SetWindowPos($handle,[IntPtr](-2),0,0,0,0,0x13)|Out-Null}
}
$checks=[Collections.Generic.List[string]]::new()
Navigate 'NavMonitor'
if(!(FindId 'StartMonitor') -or (FindId 'SaveSettings') -or (FindId 'RuntimeLog')){throw '监控页信息没有分离'}
$checks.Add('monitor-page-isolated');Shot 'monitor'
Navigate 'NavAdd'
if(!(FindId 'AddTarget') -or (FindId 'StartMonitor') -or (FindId 'SaveSettings')){throw '添加目标页信息没有分离'}
$checks.Add('add-page-isolated');Shot 'add'
Navigate 'NavSettings'
if(!(FindId 'SaveSettings') -or !(FindId 'StartupToggle') -or (FindId 'AddTarget')){throw '原生设置入口或分类异常'}
$checks.Add('native-settings-page-isolated');Shot 'settings'
Navigate 'NavLogs'
if(!(FindId 'RuntimeLog') -or (FindId 'SaveSettings')){throw '运行记录没有独立'}
$checks.Add('log-page-isolated');Shot 'logs'
Navigate 'NavMonitor'
if(!(FindId 'StartMonitor')){throw '返回缓存监控页失败'}
$checks.Add('cached-navigation-returns')
$bar=[NavigationCheck]::FindWindow('Shell_TrayWnd','')
$script:widget=[IntPtr]::Zero
[NavigationCheck]::EnumChildWindows($bar,{param($h,$l)$cls=[Text.StringBuilder]::new(256);[NavigationCheck]::GetClassName($h,$cls,256)|Out-Null;if($cls.ToString() -eq "IphoneStockTaskbar_$ProcessId"){$script:widget=$h};return $true},[IntPtr]::Zero)|Out-Null
$rect=New-Object NavigationCheck+Rect;[NavigationCheck]::GetWindowRect($widget,[ref]$rect)|Out-Null
$widthDip=($rect.Right-$rect.Left)*96/[NavigationCheck]::GetDpiForWindow($bar)
if([Math]::Abs($widthDip-108) -gt 1){throw "任务栏宽度异常：$widthDip DIP"};$checks.Add('taskbar-108-DIP')
$region="$($rect.Left),$($rect.Top),$($rect.Right-$rect.Left),$($rect.Bottom-$rect.Top)"
$shot=& C:/Users/zhang/.codex/skills/screenshot/scripts/take_screenshot.ps1 -Mode temp -Region $region
Copy-Item -LiteralPath $shot -Destination (Join-Path $evidence 'taskbar.png')
# Exercise compact navigation at a smaller physical window size, then restore it.
[NavigationCheck]::SetWindowPos($handle,[IntPtr]::Zero,0,0,860,660,0x16)|Out-Null;Start-Sleep -Milliseconds 500
Navigate 'NavSettings';if(!(FindId 'SaveSettings')){throw '紧凑窗口设置页不可达'}
$checks.Add('compact-navigation-accessible');Shot 'compact-settings'
[NavigationCheck]::SetWindowPos($handle,[IntPtr]::Zero,0,0,1120,760,0x16)|Out-Null;Navigate 'NavMonitor'
if($Exercise){
 $runPath='HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
 $startupBefore=(Get-ItemProperty $runPath -Name QingdaoIphoneStock -ErrorAction SilentlyContinue).QingdaoIphoneStock
 ClickId 'CheckOnce'
 $completed=$false
 for($i=0;$i -lt 50;$i++){
  Start-Sleep -Seconds 1
  $done=$window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'单次查询完成'))
  if($done){$completed=$true;break}
 }
 if(!$completed){throw '新版查询未完成'};$checks.Add('live-query-completes')
 ClickId 'StartMonitor';Start-Sleep -Seconds 1;ClickId 'StartMonitor';Start-Sleep -Seconds 1
 $paused=$window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'已暂停'))
 if(!$paused){throw '暂停失败'};$checks.Add('start-and-pause-work')
 Navigate 'NavAdd'
 $color=$window.FindFirst($scope,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'颜色')))
 Choose $color '夜空色'
 if((FindId 'ProductPreviewDetails').Current.Name -notmatch '夜空色'){throw '配色预览未切换'}
 Shot 'add-night-sky';Choose $color '星光白色';$checks.Add('product-color-preview-switches')
 ClickId 'AddTarget'
 $duplicate=$window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'已在监控列表中'))
 if(!$duplicate){throw '重复目标未反馈'};$checks.Add('duplicate-target-rejected')
 Navigate 'NavSettings';ClickId 'SaveSettings'
 $switch=FindId 'TaskbarToggle';$switch.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle();ClickId 'SaveSettings';Start-Sleep -Milliseconds 500
 if([NavigationCheck]::IsWindowVisible($widget)){throw '任务栏关闭开关失败'}
 $switch.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle();ClickId 'SaveSettings';Start-Sleep -Milliseconds 500
 if(![NavigationCheck]::IsWindowVisible($widget)){throw '任务栏恢复失败'};$checks.Add('taskbar-settings-toggle-works')
 $startupAfter=(Get-ItemProperty $runPath -Name QingdaoIphoneStock -ErrorAction SilentlyContinue).QingdaoIphoneStock
 if($startupBefore -ne $startupAfter){throw '隔离测试修改了正式开机启动'};$checks.Add('production-startup-unchanged')
 Navigate 'NavLogs'
 $items=(FindId 'RuntimeLog').FindAll($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem))
 $value=($items|ForEach-Object {$_.Current.Name}) -join "`n"
 if($value -notmatch '青岛' -or $value -notmatch 'iPhone Duo'){throw '运行记录未显示查询'};$checks.Add('logs-receive-query-results');Shot 'logs-after-query'
 Choose (FindId 'LogFilter') '查询'
 $items=(FindId 'RuntimeLog').FindAll($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem))
 if($items.Count -eq 0 -or @($items|Where-Object {$_.Current.Name -notmatch '\[查询\]'}).Count -gt 0){throw '记录类型筛选失败'};$checks.Add('log-category-filter-works');Shot 'logs-query-filter'
 (FindId 'LogSearch').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('不存在的记录XYZ')
 Start-Sleep -Milliseconds 200
 $items=(FindId 'RuntimeLog').FindAll($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem))
 if($items.Count -ne 0){throw '记录搜索未筛选'};$checks.Add('log-search-empty-state-works');Shot 'logs-empty-search'
 (FindId 'LogSearch').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('')
 Choose (FindId 'LogFilter') '全部类型'
 ClickId 'CopyLogs';Start-Sleep -Milliseconds 150
 if((Get-Clipboard -Raw) -notmatch 'iPhone Duo'){throw '复制当前记录列表失败'};$checks.Add('log-copy-works')
 ClickId 'ClearLogs'
 $items=(FindId 'RuntimeLog').FindAll($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem))
 if($items.Count -ne 0){throw '清空记录失败'};$checks.Add('log-clear-works')
 Navigate 'NavMonitor';Shot 'monitor-after-query'
 [NavigationCheck]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 400
 if([NavigationCheck]::IsWindowVisible($handle)){throw '关窗没有隐藏'}
 [NavigationCheck]::PostMessage($widget,0x202,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 500
 if(![NavigationCheck]::IsWindowVisible($handle)){throw '任务栏点击未恢复窗口'};$checks.Add('close-to-tray-and-click-restore')
}
@{timestamp=(Get-Date -Format o);checks=$checks;taskbarWidthDip=$widthDip;windowHandle=$handle.ToInt64();processId=$ProcessId}|ConvertTo-Json -Depth 4|Tee-Object -FilePath (Join-Path $evidence 'navigation-checks.json')
