$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $env:GH_TOKEN -or -not $env:GITHUB_REPOSITORY) { throw 'Run this script in the GitHub release workflow.' }
Set-Location -LiteralPath $repo
$plugin = Get-Content -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget/.codex-plugin/plugin.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$payload = Get-Content -LiteralPath (Join-Path $repo 'plugins/codex-quota-widget/widget/payload.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$tag = 'plugin-v' + $plugin.version
$title = "Codex Quota Widget $($payload.version) / Plugin $($plugin.version) · For ChatGPT Plus"
$api = "$($env:GITHUB_API_URL)/repos/$($env:GITHUB_REPOSITORY)"
$headers = @{ Authorization = "Bearer $env:GH_TOKEN"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$body = Get-Content -LiteralPath (Join-Path $repo 'docs/RELEASE_NOTES.md') -Raw -Encoding UTF8
$existingRelease = $null
try {
    $existingRelease = Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers
} catch {
    if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
}
if ($existingRelease) {
    $update = @{ name = $title; body = $body } | ConvertTo-Json
    $updated = Invoke-RestMethod -Method Patch -Uri "$api/releases/$($existingRelease.id)" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($update))
    if ($updated.body -ne $body) { throw 'Release introduction verification failed.' }
    Write-Output "Updated introduction: $($updated.html_url)"
    exit 0
}
if (-not $env:PROVENANCE_BUNDLE -or -not (Test-Path -LiteralPath $env:PROVENANCE_BUNDLE -PathType Leaf)) { throw 'A signed provenance bundle is required before publishing a new release.' }
$dist = Join-Path $repo 'dist'
if (-not (Test-Path -LiteralPath (Join-Path $dist 'SHA256SUMS.txt'))) { throw 'Build and package the release before publishing.' }
Copy-Item -LiteralPath $env:PROVENANCE_BUNDLE -Destination (Join-Path $dist 'build-provenance.sigstore.json')
$head = (& git rev-parse HEAD).Trim()
$request = @{ tag_name = $tag; target_commitish = $head; name = $title; body = $body; draft = $true; prerelease = $false } | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($request))
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $repo 'dist') -File)) {
    $uploadUri = ($release.upload_url -replace '\{.*$', '') + '?name=' + [Uri]::EscapeDataString($file.Name)
    $asset = Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -ContentType 'application/octet-stream' -InFile $file.FullName
    if ($asset.state -ne 'uploaded' -or $asset.size -ne $file.Length) { throw "Upload verification failed: $($file.Name)" }
    if ($asset.digest -and $asset.digest -ne ('sha256:' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())) { throw "Remote checksum mismatch: $($file.Name)" }
}
$result = Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
Write-Output "Published $($result.html_url)"
