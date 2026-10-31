param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo 'src\CodexQuotaWidget\CodexQuotaWidget.csproj'
$build=Join-Path $repo '.build\publish'
$payload=Join-Path $repo 'plugins\codex-quota-widget\widget'
if(-not(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 8 SDK on Windows is required.' }
& dotnet publish $project -c Release -o $build -p:DebugType=none -p:DebugSymbols=false -p:Deterministic=true -p:IncludeSourceRevisionInInformationalVersion=false
if($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$version=([xml](Get-Content -LiteralPath $project -Raw -Encoding UTF8)).Project.PropertyGroup.Version | Where-Object {$_}
$files=@('CodexQuotaWidget.exe','CodexQuotaWidget.dll','CodexQuotaWidget.deps.json','CodexQuotaWidget.runtimeconfig.json','Fonts/OFL-GlowSans.txt','Fonts/OFL-Orbitron.txt')
foreach($relative in $files) {
    $target=Join-Path $payload $relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    Copy-Item -LiteralPath (Join-Path $build $relative) -Destination $target -Force
}
# Preserve only documented runtime assets, never enumerate arbitrary developer output.
$manifest=Get-Content -LiteralPath (Join-Path $payload 'payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$records=@()
foreach($entry in $manifest.files) {
    $file=Join-Path $payload $entry.path
    $records+=[ordered]@{path=$entry.path;size=(Get-Item -LiteralPath $file).Length;sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest.version=[string]$version
$manifest.files=$records
[IO.File]::WriteAllText((Join-Path $payload 'payload.json'),($manifest | ConvertTo-Json -Depth 6),(New-Object Text.UTF8Encoding($false)))
Write-Output ('Built desktop '+$version+' and refreshed payload checksums.')
