param(
    [string]$LanHost = $env:LAN_HOST,
    [int]$Port = 8080,
    [string]$Scheme = "http",
    [string]$BaseUrl = ""
)

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    if ([string]::IsNullOrWhiteSpace($LanHost)) {
        $LanHost = "localhost"
    }

    $defaultPort =
        ($Scheme -eq "http" -and $Port -eq 80) -or
        ($Scheme -eq "https" -and $Port -eq 443)

    $authority = if ($defaultPort) { $LanHost } else { "${LanHost}:${Port}" }
    $BaseUrl = "${Scheme}://${authority}"
}

$base = $BaseUrl.TrimEnd("/")
Write-Host "Checking $base/health"
Invoke-RestMethod "$base/health" | Out-Host

Write-Host "Checking $base/api/conflicts/topics"
Invoke-RestMethod "$base/api/conflicts/topics" | ConvertTo-Json -Depth 5 | Out-Host

Write-Host "SignalR endpoint negotiation"
Invoke-RestMethod -Method Post "$base/hubs/conflicts/negotiate?negotiateVersion=1" | ConvertTo-Json -Depth 5 | Out-Host
