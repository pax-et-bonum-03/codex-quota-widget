param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputPath) { $OutputPath = Join-Path $repo 'dist' }
[void][IO.Directory]::CreateDirectory($OutputPath)
$payload = Join-Path $repo 'plugins/codex-quota-widget/widget'
$manifest = Get-Content -LiteralPath (Join-Path $payload 'payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$plugin = Get-Content -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget/.codex-plugin/plugin.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$pluginVersion = [string]$plugin.version
$desktopVersion = [string]$manifest.version
foreach ($entry in $manifest.files) {
    $file = Join-Path $payload $entry.path
    if ((Get-Item -LiteralPath $file).Length -ne $entry.size -or
        (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Payload verification failed: $($entry.path)"
    }
}
$files = @(git -c core.quotepath=false -C $repo ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate release files.' }
foreach ($relative in $files) {
    if ($relative -match '(^|/)(auth\.json|settings\.json|position\.json|runtime\.json|install\.json|data|bin|obj|\.build|\.release-inputs)(/|$)' -or $relative -match '\.pdb$') {
        throw "Unexpected state or build file: $relative"
    }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-Archive([string]$Name, [string]$Prefix, [string[]]$Selected, [string]$StripPrefix = '') {
    $path = Join-Path $OutputPath $Name
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in $Selected) {
            $member = $relative.Substring($StripPrefix.Length)
            if ($Prefix) { $member = "$Prefix/$member" }
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $repo $relative), $member, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose() }
    $check = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        if ($check.Entries.Count -ne $Selected.Count) { throw "Incomplete archive: $Name" }
        foreach ($entry in $check.Entries) {
            if ($entry.FullName.StartsWith('/') -or $entry.FullName.Split('/') -contains '..') { throw 'Invalid archive path.' }
            $stream = $entry.Open()
            try { $stream.CopyTo([IO.Stream]::Null) } finally { $stream.Dispose() }
        }
    } finally { $check.Dispose() }
    return $path
}
$windowsRoots = @('README.md','LICENSE','LICENSE.zh-CN.md','THIRD_PARTY_NOTICES.md','PRIVACY.md','SECURITY.md','CHANGELOG.md','PUBLISHING.md','widget.ps1','Start.cmd')
$windowsFiles = @($files | Where-Object { $_ -match '^(plugins|docs)/' -or $_ -in $windowsRoots -or $_ -eq 'tests/README.md' })
$pluginFiles = @($files | Where-Object { $_.StartsWith('plugins/codex-quota-widget/') })
$packages = @(
    Write-Archive "CodexQuotaWidget-$desktopVersion-windows-x64.zip" "CodexQuotaWidget-$desktopVersion" $windowsFiles
    Write-Archive "codex-quota-widget-$pluginVersion-plugin.zip" '' $pluginFiles 'plugins/codex-quota-widget/'
    Write-Archive "codex-quota-widget-$pluginVersion-source.zip" 'codex-quota-widget' $files
)
$lines = @($packages | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) })
[IO.File]::WriteAllText((Join-Path $OutputPath 'SHA256SUMS.txt'), ($lines -join "`n") + "`n", [Text.Encoding]::ASCII)
Get-ChildItem -LiteralPath $OutputPath -File | Select-Object Name, Length
