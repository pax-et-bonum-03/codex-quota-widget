param([string]$TestDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $TestDirectory) { $TestDirectory=Join-Path $repo ('.build/install-tests-'+[guid]::NewGuid().ToString('N')) }
$testRoot=[IO.Path]::GetFullPath($TestDirectory)
if(Test-Path -LiteralPath $testRoot) { throw 'Use a fresh test directory.' }
[void][IO.Directory]::CreateDirectory($testRoot)
$helper=Join-Path $repo 'plugins/codex-quota-widget/skills/quota-widget/scripts/widget.ps1'
$shell=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
$target=Join-Path $testRoot '中文 空格安装'
$expectedVersion=(Get-Content -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget/widget/payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
$checks=New-Object 'System.Collections.Generic.List[string]'
function Assert-Check($Condition,[string]$Name) { if(-not $Condition) { throw $Name }; $checks.Add($Name) }
function Invoke-Helper([string]$Action,[string]$Root=$target,[string]$Script=$helper,[switch]$Preview) {
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$Script,'-Action',$Action,'-DataRoot',$Root)
    if($Preview) { $arguments+='-Preview' }
    $output=& $shell @arguments
    $code=$LASTEXITCODE
    $result=($output -join "`n") | ConvertFrom-Json
    $result | Add-Member NoteProperty exitCode $code
    return $result
}
function Write-Utf8([string]$Path,[string]$Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path,$Text,(New-Object Text.UTF8Encoding($false)))
}
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupBefore=if(Test-Path -LiteralPath $run){(Get-ItemProperty -LiteralPath $run).CodexQuotaWidget}else{$null}
try {
    $status=Invoke-Helper status
    Assert-Check ($status.ok -and -not $status.installed -and -not(Test-Path -LiteralPath $target)) 'status makes no installation'
    $installed=Invoke-Helper install
    Assert-Check ($installed.ok -and $installed.installed -and $installed.version -eq $expectedVersion) 'Windows PowerShell 5.1 installs Chinese and spaced paths'
    $data=Join-Path $target 'data'
    Write-Utf8 (Join-Path $data 'settings.json') '{"language":"en"}'
    Write-Utf8 (Join-Path $data 'position.json') '{"centerX":812,"top":122}'
    Write-Utf8 (Join-Path $data 'Languages/ja-XTEST.json') '{"schemaVersion":1,"id":"ja-XTEST","displayName":"Test","strings":{}}'
    $preferences=@('settings.json','position.json','Languages/ja-XTEST.json')
    $before=@{}; foreach($file in $preferences) {$before[$file]=(Get-FileHash -LiteralPath (Join-Path $data $file)).Hash}
    $marker=Join-Path $target 'install.json'
    $owner=Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
    $owner.version='1.4.2'
    Write-Utf8 $marker ($owner | ConvertTo-Json -Depth 5)
    $upgraded=Invoke-Helper install
    Assert-Check ($upgraded.ok -and $upgraded.version -eq $expectedVersion) 'upgrade replaces previous version'
    foreach($file in $preferences) { Assert-Check ((Get-FileHash -LiteralPath (Join-Path $data $file)).Hash -eq $before[$file]) ('upgrade preserves '+$file) }

    # Tampering is exercised on a copied fixture, never the real candidate payload.
    $fixture=Join-Path $testRoot 'corrupt-plugin'
    Copy-Item -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget') -Destination $fixture -Recurse
    [IO.File]::AppendAllText((Join-Path $fixture 'widget/CodexQuotaWidget.exe'),'bad')
    $corruptHelper=Join-Path $fixture 'skills/quota-widget/scripts/widget.ps1'
    $exe=Join-Path $target 'app/CodexQuotaWidget.exe'
    $originalHash=(Get-FileHash -LiteralPath $exe).Hash
    $failed=Invoke-Helper install -Script $corruptHelper
    Assert-Check (-not $failed.ok -and $failed.exitCode -eq 1 -and $failed.error -match 'integrity') 'corrupt payload fails before changing installation'
    Assert-Check ((Get-FileHash -LiteralPath $exe).Hash -eq $originalHash) 'integrity rejection leaves program intact'

    $unowned=Join-Path $testRoot 'unowned'
    Write-Utf8 (Join-Path $unowned 'app/user-file.txt') 'keep'
    $rejected=Invoke-Helper install -Root $unowned
    Assert-Check (-not $rejected.ok -and (Get-Content -LiteralPath (Join-Path $unowned 'app/user-file.txt')) -eq 'keep') 'unowned app directory is preserved'
    $malformed=Join-Path $testRoot 'malformed-owner'
    Write-Utf8 (Join-Path $malformed 'install.json') '{"appId":"codex-quota-widget","version":"1","files":["../../do-not-touch.txt"]}'
    Write-Utf8 (Join-Path $testRoot 'do-not-touch.txt') 'keep'
    $rejected=Invoke-Helper uninstall -Root $malformed
    Assert-Check (-not $rejected.ok -and (Get-Content -LiteralPath (Join-Path $testRoot 'do-not-touch.txt')) -eq 'keep') 'escaped owner path rejected before deletion'

    $foreign=@(Get-Process -Name CodexQuotaWidget -ErrorAction SilentlyContinue)
    if($foreign.Count) { throw 'Close other widget copies before the preview lifecycle check.' }
    $started=Invoke-Helper start -Preview
    Assert-Check ($started.ok -and $started.running -and $started.processIds.Count -eq 1) 'preview window starts without account connection'
    $again=Invoke-Helper start -Preview
    Assert-Check ($again.ok -and $again.processIds[0] -eq $started.processIds[0]) 'repeated start reuses own process'
    $stopped=Invoke-Helper stop
    Assert-Check ($stopped.ok -and -not $stopped.running) 'own preview process closes gracefully'
    Write-Utf8 (Join-Path $target 'app/user-file.txt') 'keep'
    $removed=Invoke-Helper uninstall
    Assert-Check ($removed.ok -and -not $removed.installed -and $removed.dataRetained) 'uninstall removes only managed application'
    foreach($file in $preferences) { Assert-Check ((Get-FileHash -LiteralPath (Join-Path $data $file)).Hash -eq $before[$file]) ('uninstall preserves '+$file) }
    Assert-Check ((Get-Content -LiteralPath (Join-Path $target 'app/user-file.txt')) -eq 'keep') 'uninstall preserves unmanaged files'
    $removedAgain=Invoke-Helper uninstall
    Assert-Check ($removedAgain.ok) 'repeated uninstall is harmless'
    $startupAfter=if(Test-Path -LiteralPath $run){(Get-ItemProperty -LiteralPath $run).CodexQuotaWidget}else{$null}
    Assert-Check ($startupBefore -eq $startupAfter) 'existing startup entry remains unchanged'
    $report=[pscustomobject]@{ok=$true;checksPassed=$checks.Count;checks=$checks;testShell='Windows PowerShell 5.1';accountConnection=$false}
    Write-Utf8 (Join-Path $testRoot 'install-checks.json') ($report | ConvertTo-Json -Depth 5)
    $report | ConvertTo-Json -Depth 5
} finally {
    [void](Invoke-Helper stop)
}
