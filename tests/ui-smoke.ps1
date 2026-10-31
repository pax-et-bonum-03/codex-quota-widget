param([string]$OutputDirectory)
$testRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $OutputDirectory) { $OutputDirectory=Join-Path $testRepo '.build/ui-checks' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$env:CODEX_QUOTA_WIDGET_DATA_DIR=Join-Path $OutputDirectory 'data'
[void][IO.Directory]::CreateDirectory($env:CODEX_QUOTA_WIDGET_DATA_DIR)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $testRepo 'plugins/codex-quota-widget/widget/CodexQuotaWidget.dll'))
$designApp = [System.Windows.Application]::new()
$designWindow = [CodexQuotaWidget.MainWindow]::new($true)
$designFlags = [System.Reflection.BindingFlags]'NonPublic,Instance'
$designChecks = [System.Collections.Generic.List[string]]::new()
function Assert-Design($condition, $name) { if (!$condition) { throw $name }; $designChecks.Add($name) }
function Invoke-Design($name,[object[]]$arguments=@()) { $designWindow.GetType().GetMethod($name,$designFlags).Invoke($designWindow,$arguments) }
function Set-DesignField($name,$value) { $designWindow.GetType().GetField($name,$designFlags).SetValue($designWindow,$value) }
function Pump-Design([int]$milliseconds) {
    $designFrame = [System.Windows.Threading.DispatcherFrame]::new()
    $designEnd = [System.Windows.Threading.DispatcherTimer]::new()
    $designEnd.Interval = [timespan]::FromMilliseconds($milliseconds)
    $designEnd.Add_Tick({ $designEnd.Stop(); $designFrame.Continue = $false })
    $designEnd.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($designFrame)
}
function Layout-Design([bool]$expanded) {
    $designWindow.SetExpanded($expanded,$false)
    $designSize = if ($expanded) {[System.Windows.Size]::new(584,390)} else {[System.Windows.Size]::new(270,50)}
    $designShell.Measure($designSize)
    $designShell.Arrange([System.Windows.Rect]::new([System.Windows.Point]::new(0,0),$designSize))
    $designShell.UpdateLayout()
}
function Save-Design($path,[double]$scale) {
    $designRender = [System.Windows.Media.Imaging.RenderTargetBitmap]::new([int]($designShell.ActualWidth*$scale),[int]($designShell.ActualHeight*$scale),96*$scale,96*$scale,[System.Windows.Media.PixelFormats]::Pbgra32)
    $designRender.Render($designShell)
    $designEncoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $designEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($designRender))
    $designStream = [System.IO.File]::Create($path)
    try { $designEncoder.Save($designStream) } finally { $designStream.Dispose() }
}
$designShell = $designWindow.FindName('Shell')
$designFive = $designWindow.FindName('FiveDial')
$designWeek = $designWindow.FindName('WeekDial')
$designSweep = $designWindow.FindName('HudSweep')
$designChrome = $designWindow.FindName('ArmorChrome')
$designLocal = [CodexQuotaWidget.Localization]::Current
$designFolder = Join-Path $OutputDirectory 'renders'
[void][System.IO.Directory]::CreateDirectory($designFolder)
$designReset = [DateTimeOffset]::UtcNow.AddHours(2).ToUnixTimeSeconds()
try {
    $designLocal.Select('zh-CN',$false)
    [void](Invoke-Design 'ApplyLanguage')
    Layout-Design $true
    Set-DesignField 'five' ([CodexQuotaWidget.Quota]::new(9,$designReset))
    Set-DesignField 'week' ([CodexQuotaWidget.Quota]::new(19,$designReset))
    Set-DesignField 'todayDate' ([CodexQuotaWidget.Metrics]::LocalDay)
    Set-DesignField 'usage' ([CodexQuotaWidget.Usage]::new(987654321,12345678,[CodexQuotaWidget.Metrics]::LocalDay,'2026-09-07'))
    [void](Invoke-Design 'UpdateDisplay')
    Assert-Design ($designFive.HasData -and $designFive.Value -eq 91 -and !$designFive.HasAnimatedProperties) '首批数据直接呈现'
    Assert-Design ($designWindow.FindName('TodayValue').FontSize -eq $designWindow.FindName('LifetimeValue').FontSize) 'token 数字字号统一'
    $designWindow.SetExpanded($false,$false)
    $designWindow.SetExpanded($true,$true)
    Assert-Design ($designFive.Value -eq 91 -and $designWeek.Value -eq 81 -and !$designFive.HasAnimatedProperties -and !$designWeek.HasAnimatedProperties) '展开不重放读数动画'
    Pump-Design 500
    Layout-Design $true
    $designFull = $designWindow.FindName('Full')
    $designTodayPoint = $designWindow.FindName('TodayValue').TranslatePoint([System.Windows.Point]::new(0,0),$designFull)
    $designLifetimePoint = $designWindow.FindName('LifetimeValue').TranslatePoint([System.Windows.Point]::new(0,0),$designFull)
    Assert-Design ($designTodayPoint.Y -eq $designLifetimePoint.Y) 'token 数值基线对齐'
    $designTodayNote = $designWindow.FindName('TodayNote').TranslatePoint([System.Windows.Point]::new(0,0),$designFull)
    $designLifetimeNote = $designWindow.FindName('LifetimeNote').TranslatePoint([System.Windows.Point]::new(0,0),$designFull)
    Assert-Design ($designTodayNote.Y -eq $designLifetimeNote.Y) 'token 说明基线对齐'
    foreach($name in @('TodayValue','LifetimeValue')) {
        $block=$designWindow.FindName($name)
        $text=[System.Windows.Media.FormattedText]::new($block.Text,[System.Globalization.CultureInfo]::InvariantCulture,[System.Windows.FlowDirection]::LeftToRight,[System.Windows.Media.Typeface]::new($block.FontFamily,$block.FontStyle,$block.FontWeight,$block.FontStretch),$block.FontSize,$block.Foreground,1.0)
        Assert-Design ($text.WidthIncludingTrailingWhitespace -le $block.ActualWidth) ('完整显示 token 和单位：'+$name)
    }
    Assert-Design ($null -eq $designWindow.FindName('WeekBar')) '周额度去除重复进度条'

    $designOldValue = $designFive.Value
    Set-DesignField 'five' ([CodexQuotaWidget.Quota]::new(25,$designReset))
    [void](Invoke-Design 'UpdateDisplay')
    if ([System.Windows.SystemParameters]::ClientAreaAnimation) {
        Assert-Design ($designFive.HasAnimatedProperties) '真实额度变化启动过渡'
        Pump-Design 150
        Assert-Design ($designFive.Value -lt $designOldValue -and $designFive.Value -gt 75) '额度过渡从旧值到新值'
        [void](Invoke-Design 'UpdateDisplay')
        Assert-Design ($designFive.Value -gt 75) '时间重绘保留进行中的过渡'
        Pump-Design 400
    }
    Assert-Design ([math]::Abs($designFive.Value-75) -lt 0.01) '额度过渡达到真实值'
    Set-DesignField 'five' ([CodexQuotaWidget.Quota]::new(100,$designReset))
    [void](Invoke-Design 'UpdateDisplay')
    Pump-Design 500
    $designZeroText = ($designWindow.FindName('MiniFive').Inlines.Text -join '')
    Assert-Design ($designFive.HasData -and [math]::Abs($designFive.Value) -lt 0.01 -and $designZeroText.Contains('0%')) '零额度保持可识别'
    Assert-Design ($designChrome.FiveWarning -and !$designChrome.WeekWarning) '低额度只提示对应装甲边缘'
    Set-DesignField 'five' $null
    [void](Invoke-Design 'UpdateDisplay')
    Assert-Design (!$designFive.HasData -and !$designFive.HasAnimatedProperties -and !$designChrome.FiveWarning) '未知额度直接清除仪表和警示'

    Set-DesignField 'refreshing' $true
    Set-DesignField 'refreshSweepShown' $false
    [void](Invoke-Design 'UpdateStatus')
    Assert-Design ($designChrome.Signal -eq [CodexQuotaWidget.WidgetSignal]::Refreshing) '边缘提示真实刷新状态'
    if ([System.Windows.SystemParameters]::ClientAreaAnimation) {
        Assert-Design ($designSweep.Visibility -eq [System.Windows.Visibility]::Visible -and $designSweep.HasAnimatedProperties) '刷新触发一次扫光'
        Pump-Design 1050
        Assert-Design ($designSweep.Visibility -eq [System.Windows.Visibility]::Collapsed -and !$designSweep.HasAnimatedProperties) '扫光结束且不循环'
        [void](Invoke-Design 'UpdateStatus')
        Assert-Design ($designSweep.Visibility -eq [System.Windows.Visibility]::Collapsed) '状态重绘不重复触发扫光'
    }
    Set-DesignField 'refreshing' $false
    Set-DesignField 'displayError' ([System.IO.IOException]::new('test'))
    [void](Invoke-Design 'UpdateStatus')
    Assert-Design ($designChrome.Signal -eq [CodexQuotaWidget.WidgetSignal]::Error -and $designWindow.FindName('Status').Text.Contains($designLocal.Text('error.connection',[object[]]@()))) '读取失败边缘与说明一致'
    Set-DesignField 'displayError' $null
    Set-DesignField 'five' ([CodexQuotaWidget.Quota]::new(9,$designReset))
    [void](Invoke-Design 'UpdateDisplay')
    [void](Invoke-Design 'UpdateStatus')
    Assert-Design ($designChrome.Signal -eq [CodexQuotaWidget.WidgetSignal]::Ready) '恢复后清除错误边缘'
    Layout-Design $false
    Assert-Design (!$designSweep.HasAnimatedProperties -and !$designWindow.FindName('CoreGlow').HasAnimatedProperties -and !$designWindow.FindName('FiveRotation').HasAnimatedProperties) '收起后停止动态装饰'

    # A long third-party translation must leave the percentage intact, at its normal size.
    $designPackPath = Join-Path $designFolder 'long-language.json'
    [System.IO.File]::WriteAllText($designPackPath,'{"schemaVersion":1,"id":"de-XTEST","displayName":"Long text test","strings":{"mini.five":"Verbleibendes Kontingent für fünf Stunden {0}","mini.week":"{0} verbleibendes Wochenkontingent"}}')
    [void]$designLocal.Install($designPackPath)
    $designLocal.Select('de-XTEST',$false)
    [void](Invoke-Design 'ApplyLanguage')
    Layout-Design $false
    foreach ($designControlName in @('MiniFive','MiniWeek')) {
        $designControl = $designWindow.FindName($designControlName)
        $designNumbers = @($designControl.Inlines | Where-Object {$_.FontWeight -eq [System.Windows.FontWeights]::SemiBold})
        $designMiniText = ($designControl.Inlines.Text -join '')
        Assert-Design ($designMiniText.Contains('%') -and $designMiniText.Contains('…') -and $designNumbers.Count -eq 1 -and $designNumbers[0].FontSize -eq 14) ('长译文保留核心比例：'+$designControlName)
        Assert-Design ($designControl.DesiredSize.Width -le 103.01) ('长译文不溢出：'+$designControlName)
    }
    Save-Design (Join-Path $designFolder 'long-language.png') 2

    $designLocal.Select('zh-CN',$false)
    [void](Invoke-Design 'ApplyLanguage')
    Set-DesignField 'usage' ([CodexQuotaWidget.Usage]::new(987654321,$null,'2026-10-01','2026-09-07'))
    [void](Invoke-Design 'UpdateDisplay')
    Assert-Design ($designWindow.FindName('TodayValue').Text -eq $designLocal.Text('today.pending',[object[]]@()) -and $designWindow.FindName('TodayValue').FontSize -eq 16) '未更新采用状态文字规则'

    foreach ($designLanguage in @('zh-CN','zh-TW','en')) {
        $designLocal.Select($designLanguage,$false)
        [void](Invoke-Design 'ApplyLanguage')
        foreach ($designExpanded in @($false,$true)) {
            Layout-Design $designExpanded
            $designMode = if ($designExpanded) {'expanded'} else {'folded'}
            foreach ($designScale in @(1.0,1.25,1.5,2.0)) {
                Save-Design (Join-Path $designFolder ($designLanguage+'-'+$designMode+'-'+[int]($designScale*100)+'.png')) $designScale
            }
        }
    }
    [pscustomobject]@{ok=$true;version=[CodexQuotaWidget.MainWindow].Assembly.GetName().Version.ToString();checks=$designChecks;renderedLanguages=@('zh-CN','zh-TW','en');renderedScalePercent=@(100,125,150,200);scaleVerification='WPF offscreen renders; no monitor configuration changes';animationEnabled=[System.Windows.SystemParameters]::ClientAreaAnimation} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'ui-checks.json') -Encoding utf8
    Write-Output ('排版、额度过渡、状态反馈、长译文及四档缩放预览检查通过：'+$designChecks.Count+' 项。')
} finally {
    $designLocal.Select('zh-CN',$false)
    $designWindow.Close()
    # This test installed only in an isolated user data directory.
    $designInstalled = Join-Path $env:CODEX_QUOTA_WIDGET_DATA_DIR 'Languages/de-XTEST.json'
    if (Test-Path -LiteralPath $designInstalled) { Remove-Item -LiteralPath $designInstalled }
}
