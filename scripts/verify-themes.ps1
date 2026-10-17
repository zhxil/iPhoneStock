param([int]$ProcessId,[string]$EvidenceDirectory='artifacts/theme-v131')
$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot 'verify-navigation.ps1') -Raw
$prefix=$source.Substring(0,$source.IndexOf('$checks='))
$prefix=$prefix.Replace('$PSScriptRoot',("'"+$PSScriptRoot.Replace("'","''")+"'"))
. ([ScriptBlock]::Create($prefix)) -ProcessId $ProcessId -EvidenceDirectory $EvidenceDirectory
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class ThemeBroadcast {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,UIntPtr wp,string text,uint flags,uint timeout,out UIntPtr result);
 public static void Update(){UIntPtr result;SendMessageTimeout((IntPtr)0xffff,0x1a,UIntPtr.Zero,"ImmersiveColorSet",2,2000,out result);}
}
'@
$themePath='HKCU:/Software/Microsoft/Windows/CurrentVersion/Themes/Personalize'
$original=(Get-ItemProperty $themePath -Name AppsUseLightTheme).AppsUseLightTheme
$captures=[Collections.Generic.List[object]]::new()
try {
 foreach($phase in @('dark','light','dark-return')){
  $value=if($phase -eq 'light'){1}else{0}
  Set-ItemProperty $themePath -Name AppsUseLightTheme -Value $value
  [ThemeBroadcast]::Update();Start-Sleep -Milliseconds 900
  foreach($page in @('Monitor','Add','Logs','Settings')){
   Navigate "Nav$page";Shot "$phase-$page"
   if($page -eq 'Monitor'){
    $label=$window.FindFirst($scope,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'iPhone Duo'))
    if(!$label){throw '验证样例中没有 iPhone Duo'}
    $r=$label.Current.BoundingRectangle;$wr=New-Object NavigationCheck+Rect;[NavigationCheck]::GetWindowRect($handle,[ref]$wr)|Out-Null
    $captures.Add(@{phase=$phase;image="$phase-$page.png";sampleX=[int]($r.X-$wr.Left-5);sampleY=[int]($r.Y-$wr.Top+3)})
   }
  }
 }
}finally{
 Set-ItemProperty $themePath -Name AppsUseLightTheme -Value $original
 [ThemeBroadcast]::Update()
}
@{timestamp=(Get-Date -Format o);originalAppsUseLightTheme=$original;restoredAppsUseLightTheme=(Get-ItemProperty $themePath -Name AppsUseLightTheme).AppsUseLightTheme;processId=$ProcessId;captures=$captures;checks=@('four-pages-dark','four-pages-light','cached-pages-dark-after-light','system-theme-restored')}|ConvertTo-Json -Depth 5|Tee-Object -FilePath (Join-Path $evidence 'theme-checks.json')
