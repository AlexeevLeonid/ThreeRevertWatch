param(
    [string]$Service = ""
)

$composeArgs = @("-f", "docker-compose.yml", "-f", "docker-compose.demo.yml", "logs", "-f", "--tail=200")
if (-not [string]::IsNullOrWhiteSpace($Service)) {
    $composeArgs += $Service
}

if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose @composeArgs
} else {
    docker compose @composeArgs
}
