param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repo
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& (Join-Path $PSScriptRoot 'build.ps1')
foreach ($test in @(@('account-isolation','AccountIsolationChecks'),@('executable-trust','ExecutableTrustChecks'))) {
    $output = Join-Path $repo ('.build/' + $test[0])
    & dotnet publish (Join-Path $repo ('tests/' + $test[0] + '/' + $test[1] + '.csproj')) -c Release -o $output
    if ($LASTEXITCODE -ne 0) { throw ('Test build failed: ' + $test[0]) }
    & (Join-Path $output ($test[1] + '.exe')) (Join-Path $repo ('.build/results-' + $test[0] + '-' + [guid]::NewGuid().ToString('N')))
    if ($LASTEXITCODE -ne 0) { throw ('Security check failed: ' + $test[0]) }
}
