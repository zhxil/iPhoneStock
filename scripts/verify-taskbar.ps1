param([int]$ProcessId)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;public static class BarVerify {
public delegate bool E(IntPtr h,IntPtr l);
[DllImport("user32.dll")]public static extern bool EnumChildWindows(IntPtr h,E e,IntPtr l);
[DllImport("user32.dll")]public static extern bool EnumWindows(E e,IntPtr l);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string c,string t);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string c,string t);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr CreateWindowEx(uint ex,string c,string t,uint style,int x,int y,int w,int h,IntPtr p,IntPtr m,IntPtr i,IntPtr a);
[DllImport("user32.dll")]public static extern bool DestroyWindow(IntPtr h);
[DllImport("user32.dll")]public static extern IntPtr GetParent(IntPtr h);
[DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")]public static extern bool IsWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
[DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
[DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
[DllImport("user32.dll")]public static extern int GetMenuItemCount(IntPtr menu);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetMenuString(IntPtr menu,uint item,StringBuilder text,int count,uint flags);
[DllImport("user32.dll")]public static extern bool GetMenuItemRect(IntPtr h,IntPtr menu,uint item,out R rect);
public struct R{public int L,T,Rt,B;}}
'@
[BarVerify]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
if([BarVerify]::FindWindow('#32768','') -ne [IntPtr]::Zero){[BarVerify]::keybd_event(27,0,0,[UIntPtr]::Zero);[BarVerify]::keybd_event(27,0,2,[UIntPtr]::Zero);Start-Sleep -Milliseconds 300}
$process=Get-Process -Id $ProcessId
$main=$process.MainWindowHandle
if (!$main) { [BarVerify]::EnumWindows({param($h,$l) $t=[Text.StringBuilder]::new(256);[BarVerify]::GetWindowText($h,$t,256)|Out-Null;if($t.ToString() -eq 'iPhone库存助手'){$script:main=$h};return $true},[IntPtr]::Zero)|Out-Null }
$bar=[BarVerify]::FindWindow('Shell_TrayWnd',$null)
$script:widget=[IntPtr]::Zero
[BarVerify]::EnumChildWindows($bar,{param($h,$l) $c=[Text.StringBuilder]::new(256);[BarVerify]::GetClassName($h,$c,256)|Out-Null;if($c.ToString() -eq "IphoneStockTaskbar_$ProcessId"){$script:widget=$h};return $true},[IntPtr]::Zero)|Out-Null
if (!$widget -or ([BarVerify]::GetParent($widget) -ne $bar) -or !([BarVerify]::IsWindowVisible($widget))) { throw "状态条未嵌入真实任务栏：widget=$widget parent=$([BarVerify]::GetParent($widget)) bar=$bar visible=$([BarVerify]::IsWindowVisible($widget))" }
$checks=[Collections.Generic.List[string]]::new();$checks.Add('native-taskbar-child-visible')
[BarVerify]::PostMessage($widget,0x202,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
Start-Sleep -Milliseconds 500
$process.Refresh();$main=$process.MainWindowHandle
function Text($h) { $s=[Text.StringBuilder]::new(256);[BarVerify]::GetWindowText($h,$s,256)|Out-Null;$s.ToString() }
function Find($parent,[string]$name) { $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name)) }
function Click($parent,[string]$name) { $e=Find $parent $name;if(!$e){throw "找不到$name"};$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function RightClickWidget { $r=New-Object BarVerify+R;[BarVerify]::GetWindowRect($widget,[ref]$r)|Out-Null;[BarVerify]::SetCursorPos(($r.L+$r.Rt)/2,($r.T+$r.B)/2)|Out-Null;[BarVerify]::mouse_event(8,0,0,0,[UIntPtr]::Zero);[BarVerify]::mouse_event(16,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 700 }
function LeftClickWidget { $r=New-Object BarVerify+R;[BarVerify]::GetWindowRect($widget,[ref]$r)|Out-Null;[BarVerify]::SetCursorPos(($r.L+$r.Rt)/2,($r.T+$r.B)/2)|Out-Null;[BarVerify]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[BarVerify]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Seconds 1 }
function ClickMenu([string]$name) {
 $popup=[BarVerify]::FindWindow('#32768','');$menu=[BarVerify]::SendMessage($popup,0x1E1,[IntPtr]::Zero,[IntPtr]::Zero)
 for($i=0;$i -lt [BarVerify]::GetMenuItemCount($menu);$i++){
  $t=[Text.StringBuilder]::new(256);[BarVerify]::GetMenuString($menu,$i,$t,256,0x400)|Out-Null
  if($t.ToString() -eq $name){$r=New-Object BarVerify+R;[BarVerify]::GetMenuItemRect([IntPtr]::Zero,$menu,$i,[ref]$r)|Out-Null;[BarVerify]::SetCursorPos(($r.L+$r.Rt)/2,($r.T+$r.B)/2)|Out-Null;[BarVerify]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[BarVerify]::mouse_event(4,0,0,0,[UIntPtr]::Zero);return}
 }
 throw "找不到原生菜单项：$name"
}
$window=[System.Windows.Automation.AutomationElement]::FromHandle($main)
[BarVerify]::PostMessage($main,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Seconds 1
LeftClickWidget
if(![BarVerify]::IsWindowVisible($main)){throw '单击状态条未恢复主窗口'};$checks.Add('click-restores-main-window')
$fixture=[BarVerify]::CreateWindowEx(0,'STATIC','StockTaskbarFixture',0x50000000,10,0,300,60,$bar,[IntPtr]::Zero,[IntPtr]::Zero,[IntPtr]::Zero)
if(!$fixture){throw '无法创建占位测试控件'}
try {
 Start-Sleep -Seconds 2;$rect=New-Object BarVerify+R;[BarVerify]::GetWindowRect($widget,[ref]$rect)|Out-Null
 if($rect.L -lt 310){throw '状态条未避让已有占位'};$checks.Add('avoids-existing-taskbar-child')
}finally{[BarVerify]::DestroyWindow($fixture)|Out-Null}
Start-Sleep -Seconds 2
RightClickWidget
$desktop=[System.Windows.Automation.AutomationElement]::RootElement
ClickMenu '开始 / 暂停监控';Start-Sleep -Seconds 3
if((Text $widget) -notmatch '监控中'){throw '任务栏右键开始监控未生效'};$checks.Add('context-menu-starts-monitor')
Click $window '暂停监控';Start-Sleep -Seconds 1
if((Text $widget) -notmatch '已暂停'){throw '状态条未显示暂停'};$checks.Add('state-follows-monitor')
RightClickWidget
ClickMenu '隐藏任务栏状态条';Start-Sleep -Seconds 1
if([BarVerify]::IsWindowVisible($widget)){throw '右键隐藏未生效'};$checks.Add('context-menu-hides-widget')
$header=Find $window '提醒与查询设置';$header.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
$switch=Find $window '在任务栏显示状态条 隐藏'
$switch.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle();Click $window '保存设置';Start-Sleep -Seconds 1
if(![BarVerify]::IsWindowVisible($widget)){throw '设置开启状态条未生效'};$checks.Add('settings-restore-widget')
$state=Text $widget
$rect=New-Object BarVerify+R;[BarVerify]::GetWindowRect($widget,[ref]$rect)|Out-Null
@{timestamp=(Get-Date -Format o);checks=$checks;state=$state;widgetHandle=$widget.ToInt64();mainHandle=$main.ToInt64();parent=$bar.ToInt64();rect=$rect}|ConvertTo-Json -Depth 6|Tee-Object -FilePath (Join-Path $root 'artifacts/taskbar-checks.json')
