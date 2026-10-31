param([string]$BuildDirectory,[string]$OutputDirectory,[switch]$ExpectFixed)
if(-not $BuildDirectory){$BuildDirectory=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) 'plugins/codex-quota-widget/widget'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) '.build/fold-transitions'}
$ExpectFixed=$true
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$env:CODEX_QUOTA_WIDGET_DATA_DIR=Join-Path $OutputDirectory 'data'
[void][IO.Directory]::CreateDirectory($env:CODEX_QUOTA_WIDGET_DATA_DIR)
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CodexQuotaWidget.dll'))
$app=[Windows.Application]::new()
$window=[CodexQuotaWidget.MainWindow]::new($true)
$chrome=$window.FindName('ArmorChrome')
$mini=$window.FindName('Mini')
$full=$window.FindName('Full')
$shell=$window.FindName('Shell')
$records=New-Object 'System.Collections.Generic.List[object]'
function Pump([int]$Milliseconds) {
    $frame=[Windows.Threading.DispatcherFrame]::new()
    $timer=[Windows.Threading.DispatcherTimer]::new()
    $timer.Interval=[timespan]::FromMilliseconds($Milliseconds)
    $timer.Add_Tick({$timer.Stop();$frame.Continue=$false})
    $timer.Start();[Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Frame([string]$Name) {
    $window.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int][math]::Ceiling($shell.ActualWidth),[int][math]::Ceiling($shell.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($shell)
    $record=[pscustomobject]@{frame=$Name;windowWidth=$window.ActualWidth;windowHeight=$window.ActualHeight;chromeWidth=$chrome.ActualWidth;chromeHeight=$chrome.ActualHeight;miniWidth=$mini.ActualWidth;miniHeight=$mini.ActualHeight;miniVisible=($mini.Visibility -eq 'Visible');fullVisible=($full.Visibility -eq 'Visible');backgroundMode=$(if($chrome.GetType().GetProperty('IsExpanded')){$chrome.IsExpanded}else{'size heuristic'})}
    $records.Add($record)
    if($ExpectFixed -and $record.miniVisible) {
        if($record.backgroundMode -ne $false -or $record.fullVisible -or $record.chromeWidth -ne 270 -or $record.chromeHeight -ne 50 -or $record.miniWidth -ne 234 -or $record.miniHeight -ne 38) { throw ('Folded background/content diverged: '+($record | ConvertTo-Json -Compress)) }
    }
    if($Name -eq 'closing-initial' -or $Name -eq 'closing-30ms' -or $Name -eq 'closed') {
        $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream=[IO.File]::Create((Join-Path $OutputDirectory ($Name+'.png')))
        try{$encoder.Save($stream)}finally{$stream.Dispose()}
    }
}
try {
    $window.Show()
    $window.SetExpanded($true,$false);Pump 20
    Frame 'opened'
    $window.SetExpanded($false,$true)
    Frame 'closing-initial'
    Pump 30;Frame 'closing-30ms'
    Pump 50;Frame 'closing-80ms'
    Pump 150;Frame 'closed'
    # Rapid reversals should not reintroduce the expanded background while content is folded.
    for($i=0;$i -lt 12;$i++) {
        $window.SetExpanded($true,$true);Pump 35
        $window.SetExpanded($false,$true);Pump 15
        Frame ('rapid-close-'+$i)
    }
    Pump 250;Frame 'final-closed'
    if($ExpectFixed) {
        $flags=[Reflection.BindingFlags]'NonPublic,Instance'
        $bounds=[System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $window.GetType().GetField('anchorPixels',$flags).SetValue($window,[Windows.Point]::new($bounds.Left+$bounds.Width/2,$bounds.Bottom-50))
        $window.GetType().GetField('defaultDock',$flags).SetValue($window,$false)
        [void]$window.GetType().GetMethod('ApplyWindowPosition',$flags).Invoke($window,@())
        $window.SetExpanded($true,$false);Pump 20
        $window.SetExpanded($false,$true)
        Frame 'upwards-closing'
        $point=$chrome.TranslatePoint([Windows.Point]::new(0,0),$shell)
        if($chrome.VerticalAlignment -ne 'Bottom' -or [math]::Abs($point.Y-($shell.ActualHeight-50)) -gt 1) { throw 'Upward collapse lost the folded anchor.' }
        Pump 250;Frame 'upwards-closed'
        $window.RestorePosition();Frame 'restored'
        if($chrome.VerticalAlignment -ne 'Top' -or $mini.VerticalAlignment -ne 'Top') { throw 'Restore did not reset folded alignment.' }
    }
    $result=[pscustomobject]@{ok=$true;fixedAssertions=$ExpectFixed.IsPresent;animationEnabled=[Windows.SystemParameters]::ClientAreaAnimation;frames=$records}
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'fold-frames.json'),($result | ConvertTo-Json -Depth 5))
    $result | ConvertTo-Json -Depth 5
} finally { $window.Close() }
