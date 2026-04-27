param(
    [string]$LanHost = $env:LAN_HOST,
    [int]$Port = 8080
)

if ([string]::IsNullOrWhiteSpace($LanHost)) {
    $LanHost = (Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
        Select-Object -First 1 -ExpandProperty IPAddress)
}

$env:LAN_HOST = $LanHost
$env:LAN_PORT = "$Port"
if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose -f docker-compose.yml -f docker-compose.lan.yml up --build
} else {
    docker compose -f docker-compose.yml -f docker-compose.lan.yml up --build
}
