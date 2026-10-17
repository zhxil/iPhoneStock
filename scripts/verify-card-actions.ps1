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
Navigate 'NavMonitor';Shot 'monitor-actions'
$buy=FindId 'OpenProductPage';$more=FindId 'TargetActions'
if($buy.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button -or $buy.Current.HelpText -notmatch 'iphone-duo/mk2p4ch/a'){throw '购买按钮或对应商品地址异常'}
if($buy.Current.BoundingRectangle.Top -le $more.Current.BoundingRectangle.Bottom){throw '购买按钮未独立分区'}
ClickId 'TargetActions';Start-Sleep -Milliseconds 200
$desktop=[System.Windows.Automation.AutomationElement]::RootElement
$remove=$desktop.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'移除 iPhone Duo 512GB 星光白色 青岛 · 青岛万象城'))
if(!$remove){throw '更多菜单无移除入口'}
$remove.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 200
$cancel=$desktop.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'取消'))
if(!$cancel){throw '移除未要求确认'}
$cancel.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 200
if(!(FindId 'OpenProductPage')){throw '取消移除后目标丢失'}
[NavigationCheck]::SetWindowPos($handle,[IntPtr]::Zero,0,0,860,660,0x16)|Out-Null;Start-Sleep -Milliseconds 300;Shot 'compact-monitor-actions'
$buy=FindId 'OpenProductPage';$more=FindId 'TargetActions'
if($buy.Current.BoundingRectangle.Top -le $more.Current.BoundingRectangle.Bottom){throw '紧凑卡片操作重叠'}
[NavigationCheck]::SetWindowPos($handle,[IntPtr]::Zero,0,0,1120,760,0x16)|Out-Null
@{timestamp=(Get-Date -Format o);processId=$ProcessId;checks=@('native-purchase-button-with-exact-SKU','purchase-action-in-separate-footer','remove-in-overflow-menu','remove-requires-confirmation','cancel-removal-keeps-target','compact-card-actions-do-not-overlap')}|ConvertTo-Json|Tee-Object -FilePath (Join-Path $evidence 'card-action-checks.json')