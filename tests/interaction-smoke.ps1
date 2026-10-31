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
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DragWindowCheck {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
'@
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $testRepo 'plugins/codex-quota-widget/widget/CodexQuotaWidget.dll'))
$dragApp = [System.Windows.Application]::new()
$dragWindow = [CodexQuotaWidget.MainWindow]::new($true)
$dragFlags = [System.Reflection.BindingFlags]'NonPublic,Instance'
$dragPassed = 0
function Assert-Drag($condition, $name) { if (!$condition) { throw $name }; $script:dragPassed++ }
function Invoke-DragMethod($name, [object[]]$arguments = @()) { $dragWindow.GetType().GetMethod($name,$dragFlags).Invoke($dragWindow,$arguments) }
function Get-DragRect {
    $dragRect = [DragWindowCheck+RECT]::new()
    [void][DragWindowCheck]::GetWindowRect([System.Windows.Interop.WindowInteropHelper]::new($dragWindow).Handle,[ref]$dragRect)
    $dragRect
}
function Set-DragAnchor([double]$x,[double]$y) {
    $dragWindow.GetType().GetField('anchorPixels',$dragFlags).SetValue($dragWindow,[System.Windows.Point]::new($x,$y))
    $dragWindow.GetType().GetField('defaultDock',$dragFlags).SetValue($dragWindow,$false)
    [void](Invoke-DragMethod 'ApplyWindowPosition')
}

$dragForeground = [DragWindowCheck]::GetForegroundWindow()
$dragWindow.Show()
$dragWindow.SetExpanded($false,$false)
$dragOriginal = Get-DragRect
$dragDpi = [System.Windows.Media.VisualTreeHelper]::GetDpi($dragWindow)
[void](Invoke-DragMethod 'BeginPointerDrag' ([object[]]@([System.Windows.Point]::new(0,0))))
Assert-Drag ($dragWindow.GetType().GetField('dragPending',$dragFlags).GetValue($dragWindow) -and $dragWindow.GetType().GetField('dragTimer',$dragFlags).GetValue($dragWindow).IsEnabled) '按下后未进入拖动状态'
[void](Invoke-DragMethod 'UpdatePointerDrag' ([object[]]@([System.Windows.Point]::new(-175,240))))
$dragMoved = Get-DragRect
Assert-Drag ($dragMoved.Left -eq $dragOriginal.Left - 175 -and $dragMoved.Top -eq $dragOriginal.Top + 240) '原生窗口没有跟随拖动'
[void](Invoke-DragMethod 'EndPointerDrag')
Assert-Drag (!$dragWindow.IsMouseCaptured -and !$dragWindow.GetType().GetField('dragTimer',$dragFlags).GetValue($dragWindow).IsEnabled) '松开后拖动未结束'
$dragWindow.SetExpanded($true,$false)
$dragOpen = Get-DragRect
Assert-Drag ([math]::Abs(($dragOpen.Left+$dragOpen.Right)/2 - ($dragMoved.Left+$dragMoved.Right)/2) -le 1 -and $dragOpen.Top -eq $dragMoved.Top) '展开后没有保持移动中心和纵向位置'
$dragWindow.SetExpanded($false,$false)
$dragFold = Get-DragRect
Assert-Drag ($dragFold.Left -eq $dragMoved.Left -and $dragFold.Top -eq $dragMoved.Top) '收起后回到了屏幕中心'
Assert-Drag ([DragWindowCheck]::GetForegroundWindow() -eq $dragForeground) '拖动抢走了原应用焦点'

$dragRestoreButton = $dragWindow.FindName('RestoreButton')
Assert-Drag ([bool]$dragWindow.GetType().GetMethod('IsInteractiveSource',[System.Reflection.BindingFlags]'NonPublic,Static').Invoke($null,[object[]]@($dragRestoreButton))) '按钮未从拖动区域中排除'
$dragWindow.SetExpanded($true,$false)
$dragRestoreButton.RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Button]::ClickEvent))
$dragRestored = Get-DragRect
Assert-Drag ($dragRestored.Left -eq $dragOriginal.Left -and $dragRestored.Top -eq $dragOriginal.Top -and $dragRestored.Right -eq $dragOriginal.Right) '一键复原按钮未回到原位置'

$dragWorkArea = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
Set-DragAnchor ($dragWorkArea.Left + $dragWorkArea.Width/2) ($dragWorkArea.Bottom - 50*$dragDpi.DpiScaleY)
$dragBottom = Get-DragRect
$dragWindow.SetExpanded($true,$false)
$dragUp = Get-DragRect
Assert-Drag ($dragUp.Top -lt $dragBottom.Top -and $dragUp.Bottom -le $dragWorkArea.Bottom + 1) '下沿未向上展开'
$dragWindow.SetExpanded($false,$false)
$dragBottomAgain = Get-DragRect
Assert-Drag ($dragBottomAgain.Top -eq $dragBottom.Top -and $dragBottomAgain.Left -eq $dragBottom.Left) '向上展开后收起位置错误'

[void](Invoke-DragMethod 'OpenMenu')
$dragMenu = $dragWindow.GetType().GetField('menu',$dragFlags).GetValue($dragWindow)
Assert-Drag ($dragMenu.Items[2].Header -eq [CodexQuotaWidget.Localization]::Current.Text('placement.reset',[object[]]@())) '右键菜单缺少复原'
$dragMenu.Items[2].RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.MenuItem]::ClickEvent))
$dragContextRestored = Get-DragRect
Assert-Drag ($dragContextRestored.Left -eq $dragOriginal.Left -and $dragContextRestored.Top -eq $dragOriginal.Top) '右键复原失效'
[void](Invoke-DragMethod 'CreateTray')
$dragTray = $dragWindow.GetType().GetField('tray',$dragFlags).GetValue($dragWindow)
Set-DragAnchor 700 200
$dragTrayReset = @($dragTray.ContextMenuStrip.Items | Where-Object {$_.Text -eq [CodexQuotaWidget.Localization]::Current.Text('placement.reset',[object[]]@())})
Assert-Drag ($dragTrayReset.Count -eq 1) '托盘菜单缺少复原'
$dragTrayReset[0].PerformClick()
$dragTrayRestored = Get-DragRect
Assert-Drag ($dragTrayRestored.Left -eq $dragOriginal.Left -and $dragTrayRestored.Top -eq $dragOriginal.Top) '托盘复原失效'

$dragNegative = [CodexQuotaWidget.WindowPlacement]::Calculate([System.Windows.Point]::new(-900,100),300,400,50,[System.Windows.Rect]::new(-1920,0,1920,1080),$false)
Assert-Drag ($dragNegative.X -eq -1050 -and $dragNegative.Y -eq 100) '负坐标屏幕计算错误'
$dragEdge = [CodexQuotaWidget.WindowPlacement]::Calculate([System.Windows.Point]::new(1900,1050),584,390,50,[System.Windows.Rect]::new(0,0,1920,1080),$true)
Assert-Drag ($dragEdge.X -eq 1336 -and $dragEdge.Y -eq 690) '边缘位置未限制在屏幕内'
$dragSavedFile = Join-Path $OutputDirectory ('placement-test-' + [guid]::NewGuid().ToString('N') + '.json')
[CodexQuotaWidget.WindowPlacement]::Save($dragSavedFile,[System.Windows.Point]::new(-700,250))
$dragSaved = [CodexQuotaWidget.WindowPlacement]::Load($dragSavedFile)
Assert-Drag ($dragSaved.X -eq -700 -and $dragSaved.Y -eq 250) '位置保存或恢复失败'
[System.IO.File]::WriteAllText($dragSavedFile,'{"centerX":1e100,"top":0}')
Assert-Drag ($null -eq [CodexQuotaWidget.WindowPlacement]::Load($dragSavedFile)) '异常位置没有回退'
[CodexQuotaWidget.WindowPlacement]::Save($dragSavedFile,$null)
Assert-Drag (!(Test-Path -LiteralPath $dragSavedFile)) '复原后未清除旧位置'
Assert-Drag ($dragWindow.Topmost -and !$dragWindow.ShowActivated -and !$dragWindow.ShowInTaskbar) '置顶或焦点设置发生变化'
[pscustomobject]@{ok=$true;version=[CodexQuotaWidget.MainWindow].Assembly.GetName().Version.ToString();checksPassed=$dragPassed;dragStateAndNativeMovement=$true;foldKeepsPosition=$true;upwardExpansion=$true;restoreButtonContextTray=$true;positionPersistence=$true;original=$dragOriginal;moved=$dragMoved;restored=$dragRestored;dpiScale=$dragDpi.DpiScaleX} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'drag-checks.json') -Encoding utf8
$dragWindow.Close()
Write-Output '拖动、展开收起位置、按钮/右键/托盘复原、焦点、边缘和位置保存检查通过。'
