param()
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:GH_TOKEN -or $env:GITHUB_REF -ne 'refs/heads/main') { throw 'Payload synchronization is restricted to the main-branch release workflow.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repo
if ((& git rev-parse HEAD).Trim() -ne $env:GITHUB_SHA) { throw 'Unexpected checkout revision.' }
$prefix = 'plugins/codex-quota-widget/widget/'
# Stage only generated managed program files, never arbitrary runner output.
$files = @('CodexQuotaWidget.exe','CodexQuotaWidget.dll','CodexQuotaWidget.deps.json','CodexQuotaWidget.runtimeconfig.json','payload.json')
& git add -- @($files | ForEach-Object { $prefix + $_ })
if ($LASTEXITCODE -ne 0) { throw 'Cannot stage verified payload.' }
& git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Output 'Verified payload already matches the repository.'; exit 0 }
if ($LASTEXITCODE -ne 1) { throw 'Cannot compare payload changes.' }
& git -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' commit -m 'Build verified desktop payload from source'
if ($LASTEXITCODE -ne 0) { throw 'Cannot record verified payload.' }
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('x-access-token:' + $env:GH_TOKEN))
Write-Output "::add-mask::$basic"
try {
    # The short-lived token stays in this process environment, not a saved Git config.
    $env:GIT_CONFIG_COUNT = '1'
    $env:GIT_CONFIG_KEY_0 = 'http.https://github.com/.extraheader'
    $env:GIT_CONFIG_VALUE_0 = 'AUTHORIZATION: basic ' + $basic
    & git push origin HEAD:refs/heads/main
    if ($LASTEXITCODE -ne 0) { throw 'Payload publication was refused; inspect the current branch before retrying.' }
} finally {
    Remove-Item Env:GIT_CONFIG_COUNT,Env:GIT_CONFIG_KEY_0,Env:GIT_CONFIG_VALUE_0 -ErrorAction SilentlyContinue
    $basic = $null
}
