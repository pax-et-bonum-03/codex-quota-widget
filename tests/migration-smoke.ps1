param([string]$TestDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $TestDirectory) { $TestDirectory=Join-Path $repo ('.build/migration-tests-'+[guid]::NewGuid().ToString('N')) }
$root=[IO.Path]::GetFullPath($TestDirectory)
if(Test-Path -LiteralPath $root) { throw 'Use a fresh directory.' }
[void][IO.Directory]::CreateDirectory($root)
$app=Join-Path $root 'legacy-app'
Copy-Item -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget/widget') -Destination $app -Recurse
$checks=New-Object 'System.Collections.Generic.List[string]'
function Assert-Check($Condition,[string]$Name) {if(-not $Condition){throw $Name};$checks.Add($Name)}
function Write-Utf8([string]$Path,[string]$Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path,$Text,(New-Object Text.UTF8Encoding($false)))
}
function Run-Check([string]$Data,[string]$Report) {
    $old=$env:CODEX_QUOTA_WIDGET_DATA_DIR
    try {
        $env:CODEX_QUOTA_WIDGET_DATA_DIR=$Data
        $process=Start-Process -FilePath (Join-Path $app 'CodexQuotaWidget.exe') -ArgumentList '--self-test',('"'+$Report+'"') -WindowStyle Hidden -PassThru -Wait
        if($process.ExitCode -ne 0) { throw 'Self-test failed.' }
        return Get-Content -LiteralPath $Report -Raw -Encoding UTF8 | ConvertFrom-Json
    } finally { $env:CODEX_QUOTA_WIDGET_DATA_DIR=$old }
}
Write-Utf8 (Join-Path $app 'settings.json') '{"language":"en","unrelated":"DO_NOT_IMPORT"}'
Write-Utf8 (Join-Path $app 'position.json') '{"centerX":720,"top":140,"unrelated":"DO_NOT_IMPORT"}'
Write-Utf8 (Join-Path $app 'Languages/a-invalid.json') 'bad json'
Write-Utf8 (Join-Path $app 'Languages/ja-XTEST.json') '{"schemaVersion":1,"id":"ja-XTEST","displayName":"Test","strings":{}}'
Write-Utf8 (Join-Path $app 'Languages/unsafe.json') '{"schemaVersion":1,"id":"../../escape","displayName":"Test","strings":{}}'
$data=Join-Path $root 'user-data'
$result=Run-Check $data (Join-Path $root 'metrics.json')
Assert-Check ($result.ok -and $result.checksPassed -eq 12) 'baseline account metric fixtures pass'
$settings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Check ($settings.language -eq 'en' -and @($settings.PSObject.Properties).Count -eq 1) 'legacy preferences import only recognized language'
$position=Get-Content -LiteralPath (Join-Path $data 'position.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Check ($position.centerX -eq 720 -and $position.top -eq 140 -and @($position.PSObject.Properties).Count -eq 2) 'legacy position imports only coordinates'
Assert-Check (Test-Path -LiteralPath (Join-Path $data 'Languages/ja-XTEST.json')) 'malformed language does not block valid language import'
Assert-Check (-not(Test-Path -LiteralPath (Join-Path $root 'escape.json'))) 'unsafe language id cannot escape data directory'
Write-Utf8 (Join-Path $data 'settings.json') '{"language":"zh-TW"}'
Write-Utf8 (Join-Path $data 'position.json') '{"centerX":911,"top":33}'
[void](Run-Check $data (Join-Path $root 'metrics-again.json'))
Assert-Check ((Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json).language -eq 'zh-TW') 'existing language overrides legacy settings'
Assert-Check ((Get-Content -LiteralPath (Join-Path $data 'position.json') -Raw -Encoding UTF8 | ConvertFrom-Json).centerX -eq 911) 'existing position overrides legacy position'
Write-Utf8 (Join-Path $app 'settings.json') 'invalid json'
$other=Join-Path $root 'bad-settings-data'
[void](Run-Check $other (Join-Path $root 'metrics-bad-settings.json'))
Assert-Check (Test-Path -LiteralPath (Join-Path $other 'position.json')) 'invalid preferences do not block position migration'
Assert-Check (Test-Path -LiteralPath (Join-Path $other 'Languages/ja-XTEST.json')) 'invalid preferences do not block language migration'
$report=[pscustomobject]@{ok=$true;checksPassed=$checks.Count;checks=$checks;metricChecks=12}
Write-Utf8 (Join-Path $root 'migration-checks.json') ($report | ConvertTo-Json -Depth 5)
$report | ConvertTo-Json -Depth 5
