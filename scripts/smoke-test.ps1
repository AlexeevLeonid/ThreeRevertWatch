param(
    [string]$LanHost = $env:LAN_HOST,
    [int]$Port = 8080
)

if ([string]::IsNullOrWhiteSpace($LanHost)) {
    $LanHost = "localhost"
}

$base = "http://${LanHost}:${Port}"
Write-Host "Checking $base/health"
Invoke-RestMethod "$base/health" | Out-Host

Write-Host "Checking $base/api/conflicts/topics"
Invoke-RestMethod "$base/api/conflicts/topics" | ConvertTo-Json -Depth 5 | Out-Host

Write-Host "SignalR endpoint negotiation"
Invoke-RestMethod -Method Post "$base/hubs/conflicts/negotiate?negotiateVersion=1" | ConvertTo-Json -Depth 5 | Out-Host

