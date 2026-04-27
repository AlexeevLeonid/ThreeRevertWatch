param(
    [string]$Service = ""
)

if ([string]::IsNullOrWhiteSpace($Service)) {
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        docker-compose -f docker-compose.yml -f docker-compose.lan.yml logs -f --tail=200
    } else {
        docker compose -f docker-compose.yml -f docker-compose.lan.yml logs -f --tail=200
    }
} else {
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        docker-compose -f docker-compose.yml -f docker-compose.lan.yml logs -f --tail=200 $Service
    } else {
        docker compose -f docker-compose.yml -f docker-compose.lan.yml logs -f --tail=200 $Service
    }
}
