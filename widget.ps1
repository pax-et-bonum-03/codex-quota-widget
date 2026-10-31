param([ValidateSet('install','start','status','stop','uninstall')][string]$Action='start')
$script=Join-Path $PSScriptRoot 'plugins/codex-quota-widget/skills/quota-widget/scripts/widget.ps1'
& $script -Action $Action
exit $LASTEXITCODE
