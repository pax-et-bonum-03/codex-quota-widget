param([string]$BuildDirectory,[string]$OutputDirectory,[switch]$RequireStable)
if(-not $BuildDirectory){$BuildDirectory=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) 'plugins/codex-quota-widget/widget'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) '.build/window-motion'}
$RequireStable=$true
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$env:CODEX_QUOTA_WIDGET_DATA_DIR=Join-Path $OutputDirectory 'data'
[void][IO.Directory]::CreateDirectory($env:CODEX_QUOTA_WIDGET_DATA_DIR)
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MotionWindowCheck {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
'@
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CodexQuotaWidget.dll'))
$app=[Windows.Application]::new()
$window=[CodexQuotaWidget.MainWindow]::new($true)
$window.IsHitTestVisible=$false
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$frames=New-Object 'System.Collections.Generic.List[object]'
$violations=New-Object 'System.Collections.Generic.List[string]'
$foreground=[MotionWindowCheck]::GetForegroundWindow()
function Bounds {
    $rect=[MotionWindowCheck+RECT]::new()
    [void][MotionWindowCheck]::GetWindowRect([Windows.Interop.WindowInteropHelper]::new($window).Handle,[ref]$rect)
    return @($rect.Left,$rect.Top,$rect.Right,$rect.Bottom)
}
function Pump([int]$Milliseconds) {
    $frame=[Windows.Threading.DispatcherFrame]::new()
    $timer=[Windows.Threading.DispatcherTimer]::new()
    $timer.Interval=[timespan]::FromMilliseconds($Milliseconds)
    $timer.Add_Tick({$timer.Stop();$frame.Continue=$false})
    $timer.Start();[Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Set-Anchor([double]$X,[double]$Y,[bool]$Dock) {
    $window.GetType().GetField('anchorPixels',$flags).SetValue($window,[Windows.Point]::new($X,$Y))
    $window.GetType().GetField('defaultDock',$flags).SetValue($window,$Dock)
    [void]$window.GetType().GetMethod('ApplyWindowPosition',$flags).Invoke($window,@())
}
function Check-Transition([string]$Name,[bool]$Expanded,[int]$Duration=220) {
    $window.SetExpanded($Expanded,$true)
    $window.UpdateLayout()
    $target=Bounds
    $control=$window.FindName($(if($Expanded){'FiveValue'}else{'MiniFive'}))
    $origin=$control.PointToScreen([Windows.Point]::new(0,0))
    $widthAnimated=[Windows.DependencyPropertyHelper]::GetValueSource($window,[Windows.FrameworkElement]::WidthProperty).IsAnimated
    $heightAnimated=[Windows.DependencyPropertyHelper]::GetValueSource($window,[Windows.FrameworkElement]::HeightProperty).IsAnimated
    if($widthAnimated -or $heightAnimated){$violations.Add($Name+': native dimensions animated')}
    $handler=[EventHandler]{
        $current=Bounds
        $point=$control.PointToScreen([Windows.Point]::new(0,0))
        $changed=($current -join ',') -ne ($target -join ',')
        $textMoved=[math]::Abs($point.X-$origin.X) -gt 0.1 -or [math]::Abs($point.Y-$origin.Y) -gt 0.1
        if($changed -or $textMoved){$violations.Add($Name+': screen bounds or text origin drift')}
        $frames.Add([pscustomobject]@{transition=$Name;bounds=$current;textX=$point.X;textY=$point.Y;drift=($changed -or $textMoved)})
    }
    [Windows.Media.CompositionTarget]::add_Rendering($handler)
    try { Pump $Duration } finally { [Windows.Media.CompositionTarget]::remove_Rendering($handler) }
    if([MotionWindowCheck]::GetForegroundWindow() -ne $foreground){$violations.Add($Name+': foreground focus changed')}
}
try {
    $window.Show();$window.UpdateLayout()
    $bounds=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $area=[System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $window.SetExpanded($false,$false)
    Set-Anchor ($bounds.Left+$bounds.Width/2) $bounds.Top $true
    Check-Transition 'top-open' $true
    Check-Transition 'top-close' $false
    Set-Anchor ($area.Left+$area.Width/2) ($area.Top+180) $false
    Check-Transition 'moved-open' $true
    Check-Transition 'moved-close' $false
    $dpi=[Windows.Media.VisualTreeHelper]::GetDpi($window)
    Set-Anchor ($area.Left+$area.Width/2) ($area.Bottom-50*$dpi.DpiScaleY) $false
    Check-Transition 'upwards-open' $true
    Check-Transition 'upwards-close' $false
    Set-Anchor ($area.Left+135*$dpi.DpiScaleX) ($area.Top+100) $false
    Check-Transition 'edge-open' $true
    Check-Transition 'edge-close' $false
    $window.RestorePosition()
    for($i=0;$i -lt 8;$i++){
        Check-Transition ('reverse-open-'+$i) $true 35
        Check-Transition ('reverse-close-'+$i) $false 35
    }
    Pump 350
    $window.RestorePosition()
    if($frames.Count -lt 20){throw 'Insufficient compositor samples.'}
    $result=[pscustomobject]@{ok=($violations.Count -eq 0);version=$window.GetType().Assembly.GetName().Version.ToString();samples=$frames.Count;driftFrames=@($frames | Where-Object {$_.drift}).Count;violations=$violations;frames=$frames;dpiScale=$dpi.DpiScaleX}
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'motion-checks.json'),($result | ConvertTo-Json -Depth 5))
    [pscustomobject]@{ok=$result.ok;version=$result.version;samples=$result.samples;driftFrames=$result.driftFrames;violations=$violations.Count;dpiScale=$result.dpiScale} | ConvertTo-Json
    if($RequireStable -and -not $result.ok){throw 'Compositor frames show unstable window or text position.'}
} finally {$window.Close()}
