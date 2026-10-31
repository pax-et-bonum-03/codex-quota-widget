param(
    [ValidateSet('install','start','status','stop','uninstall')][string]$Action='status',
    [string]$DataRoot,
    [switch]$Preview
)
$ErrorActionPreference='Stop'
$quotaPluginRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if(-not $DataRoot) { $DataRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CodexQuotaWidget' }
$quotaRoot=[IO.Path]::GetFullPath($DataRoot).TrimEnd([char[]]'\/')
$quotaApp=Join-Path $quotaRoot 'app'
$quotaData=Join-Path $quotaRoot 'data'
$quotaMarker=Join-Path $quotaRoot 'install.json'
$quotaExe=Join-Path $quotaApp 'CodexQuotaWidget.exe'
$quotaPayload=Join-Path $quotaPluginRoot 'widget'

function Assert-SafePath([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path)
    if($quotaRoot -eq [IO.Path]::GetPathRoot($quotaRoot).TrimEnd([char[]]'\/') -or
       $quotaRoot -eq [Environment]::GetFolderPath('UserProfile')) { throw 'Unsafe installation root.' }
    if($full -ne $quotaRoot -and -not $full.StartsWith($quotaRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes installation root.' }
    $current=$full
    while($current) {
        if(Test-Path -LiteralPath $current) {
            if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installation paths cannot contain links or junctions.' }
        }
        $parent=[IO.Path]::GetDirectoryName($current)
        if($parent -eq $current) { break }
        $current=$parent
    }
    return $full
}
function Resolve-ManagedPath([string]$Relative) {
    if([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
       $Relative.Contains(':') -or @($Relative -split '[\\/]' | Where-Object {$_ -eq '..' -or $_ -eq '.'}).Count) { throw 'Invalid managed file path.' }
    if($Relative -match '(^|[\\/])(settings\.json|position\.json|Languages)([\\/]|$)') { throw 'Preferences cannot be managed application files.' }
    return Assert-SafePath (Join-Path $quotaApp $Relative)
}
function Read-Owner {
    [void](Assert-SafePath $quotaMarker)
    if(-not(Test-Path -LiteralPath $quotaMarker)) { return $null }
    if((Get-Item -LiteralPath $quotaMarker).Length -gt 65536) { throw 'Invalid installation record.' }
    $owner=Get-Content -LiteralPath $quotaMarker -Raw -Encoding UTF8 | ConvertFrom-Json
    if($owner.appId -ne 'codex-quota-widget' -or -not $owner.files) { throw 'Directory belongs to another application.' }
    foreach($relative in $owner.files) { [void](Resolve-ManagedPath ([string]$relative)) }
    return $owner
}
function Get-QuotaProcesses {
    @(Get-Process -Name CodexQuotaWidget -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -eq $quotaExe })
}
function Stop-Quota {
    if(-not('QuotaReleaseWindow' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QuotaReleaseWindow {
 public delegate bool Callback(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumThreadWindows(uint thread,Callback callback,IntPtr value);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
}
'@
    }
    foreach($process in @(Get-QuotaProcesses)) {
        $close=[QuotaReleaseWindow+Callback]{param($handle,$unused)
            if([QuotaReleaseWindow]::IsWindowVisible($handle)) { [void][QuotaReleaseWindow]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
            return $true
        }
        foreach($thread in $process.Threads) { [void][QuotaReleaseWindow]::EnumThreadWindows([uint32]$thread.Id,$close,[IntPtr]::Zero) }
        if(-not $process.WaitForExit(5000)) { throw 'Widget has not exited. Exit from its tray menu, then try again.' }
    }
}
function Assert-Runtime {
    if(-not [Environment]::Is64BitOperatingSystem) { throw 'Windows x64 is required.' }
    $candidates=@()
    foreach($folder in @($env:ProgramW6432,$env:ProgramFiles)) { if($folder) { $candidates+=Join-Path $folder 'dotnet\dotnet.exe' } }
    $command=Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if($command) { $candidates+=$command.Source }
    foreach($candidate in $candidates | Select-Object -Unique) {
        if(-not(Test-Path -LiteralPath $candidate)) { continue }
        $runtimes=@(& $candidate --list-runtimes 2>$null)
        if($LASTEXITCODE -eq 0 -and ($runtimes -match '^Microsoft.WindowsDesktop.App 8\.') -and ($runtimes -match '^Microsoft.NETCore.App 8\.')) { return }
    }
    throw 'Install Microsoft .NET 8 Desktop Runtime (Windows x64): https://dotnet.microsoft.com/download/dotnet/8.0'
}
function Install-Quota {
    Assert-Runtime
    [void](Assert-SafePath $quotaRoot)
    $owner=Read-Owner
    if(-not $owner -and (Test-Path -LiteralPath $quotaApp) -and @(Get-ChildItem -LiteralPath $quotaApp -Force).Count) { throw 'Installation directory is not owned by this widget. Choose an empty DataRoot.' }
    $manifest=Get-Content -LiteralPath (Join-Path $quotaPayload 'payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($manifest.appId -ne 'codex-quota-widget' -or $manifest.version -notmatch '^\d+\.\d+\.\d+$' -or -not $manifest.files) { throw 'Invalid payload manifest.' }
    $relativeFiles=@($manifest.files | ForEach-Object { [string]$_.path })
    if(@($relativeFiles | Select-Object -Unique).Count -ne $relativeFiles.Count -or 'CodexQuotaWidget.exe' -notin $relativeFiles) { throw 'Invalid or duplicate payload files.' }
    foreach($file in $manifest.files) {
        [void](Resolve-ManagedPath ([string]$file.path))
        $source=Join-Path $quotaPayload ([string]$file.path)
        if(-not(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $file.sha256 -or (Get-Item -LiteralPath $source).Length -ne $file.size) { throw ('Payload integrity check failed: '+$file.path) }
    }
    # Verify every source before stopping or replacing any installed application file.
    Stop-Quota
    [void][IO.Directory]::CreateDirectory($quotaApp)
    [void][IO.Directory]::CreateDirectory($quotaData)
    $previous=@{}
    $oldMarker=if(Test-Path -LiteralPath $quotaMarker) { [IO.File]::ReadAllBytes($quotaMarker) } else { $null }
    $allFiles=@($relativeFiles)
    if($owner) { $allFiles+=@($owner.files) }
    foreach($relative in $allFiles | Select-Object -Unique) {
        $target=Resolve-ManagedPath $relative
        $previous[$target]=if(Test-Path -LiteralPath $target) { [IO.File]::ReadAllBytes($target) } else { $null }
    }
    try {
        foreach($file in $manifest.files) {
            $target=Resolve-ManagedPath ([string]$file.path)
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.File]::Copy((Join-Path $quotaPayload ([string]$file.path)),$target,$true)
        }
        if($owner) {
            foreach($relative in @($owner.files)) {
                if($relative -notin $relativeFiles) {
                    $target=Resolve-ManagedPath $relative
                    if(Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
                }
            }
        }
        $record=[ordered]@{appId='codex-quota-widget';version=$manifest.version;files=$relativeFiles}
        [IO.File]::WriteAllText($quotaMarker,($record | ConvertTo-Json -Depth 4),(New-Object Text.UTF8Encoding($false)))
    } catch {
        foreach($target in $previous.Keys) {
            [void](Assert-SafePath $target)
            if($null -eq $previous[$target]) { if(Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force } }
            else { [IO.File]::WriteAllBytes($target,$previous[$target]) }
        }
        if($null -ne $oldMarker) { [IO.File]::WriteAllBytes($quotaMarker,$oldMarker) }
        elseif(Test-Path -LiteralPath $quotaMarker) { Remove-Item -LiteralPath $quotaMarker -Force }
        throw
    }
}
try {
    [void](Assert-SafePath $quotaRoot)
    $owner=Read-Owner
    if($Action -eq 'install') { Install-Quota }
    elseif($Action -eq 'start') {
        $foreign=@(Get-Process -Name CodexQuotaWidget -ErrorAction SilentlyContinue | Where-Object { -not $_.Path -or $_.Path -ne $quotaExe })
        if($foreign.Count) { throw 'Another copy of the widget is running. Exit it from its tray menu first.' }
        $payloadVersion=(Get-Content -LiteralPath (Join-Path $quotaPayload 'payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
        if(-not $owner -or $owner.version -ne $payloadVersion -or -not(Test-Path -LiteralPath $quotaExe)) { Install-Quota }
        Assert-Runtime
        if(@(Get-QuotaProcesses).Count -eq 0) {
            $previousData=$env:CODEX_QUOTA_WIDGET_DATA_DIR
            try {
                $env:CODEX_QUOTA_WIDGET_DATA_DIR=$quotaData
                $start=@{FilePath=$quotaExe;WorkingDirectory=$quotaApp;WindowStyle='Hidden';PassThru=$true}
                if($Preview) { $start.ArgumentList='--preview-window' }
                [void](Start-Process @start)
            } finally { $env:CODEX_QUOTA_WIDGET_DATA_DIR=$previousData }
            Start-Sleep -Milliseconds 1600
            if(@(Get-QuotaProcesses).Count -eq 0) { throw 'Widget did not remain running. Check prerequisites and Codex login.' }
        }
    } elseif($Action -eq 'stop') { Stop-Quota }
    elseif($Action -eq 'uninstall' -and $owner) {
        Stop-Quota
        $run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        if(Test-Path -LiteralPath $run) {
            $entry=(Get-ItemProperty -LiteralPath $run -ErrorAction Stop).CodexQuotaWidget
            if($entry -eq ('"'+$quotaExe+'"')) { Remove-ItemProperty -LiteralPath $run -Name CodexQuotaWidget }
        }
        foreach($relative in $owner.files) {
            $target=Resolve-ManagedPath $relative
            if(Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
        }
        Remove-Item -LiteralPath $quotaMarker -Force
        # Remove only empty known application directories. User data is always retained.
        foreach($folder in @((Join-Path $quotaApp 'Fonts'),$quotaApp)) {
            [void](Assert-SafePath $folder)
            if((Test-Path -LiteralPath $folder -PathType Container) -and @(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) { Remove-Item -LiteralPath $folder }
        }
    }
    $owner=Read-Owner
    $processes=@(Get-QuotaProcesses)
    [pscustomobject]@{ok=$true;action=$Action;installed=($null -ne $owner -and (Test-Path -LiteralPath $quotaExe));running=($processes.Count -gt 0);version=$(if($owner){$owner.version}else{$null});processIds=@($processes | ForEach-Object {$_.Id});appDirectory=$quotaApp;dataDirectory=$quotaData;dataRetained=($Action -eq 'uninstall')} | ConvertTo-Json -Compress
} catch {
    [pscustomobject]@{ok=$false;action=$Action;error=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 1
}
