if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose -f docker-compose.yml -f docker-compose.lan.yml down
} else {
    docker compose -f docker-compose.yml -f docker-compose.lan.yml down
}
