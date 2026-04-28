param(
    [string]$PublicHost = $env:PUBLIC_HOST,
    [switch]$Foreground
)

if ([string]::IsNullOrWhiteSpace($PublicHost)) {
    throw "Set PUBLIC_HOST or pass -PublicHost demo.example.com"
}

$env:PUBLIC_HOST = $PublicHost
$composeArgs = @("-f", "docker-compose.yml", "-f", "docker-compose.demo.yml", "up", "--build")
if (-not $Foreground) {
    $composeArgs += "-d"
}

if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose @composeArgs
} else {
    docker compose @composeArgs
}
