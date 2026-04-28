if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose -f docker-compose.yml -f docker-compose.demo.yml down
} else {
    docker compose -f docker-compose.yml -f docker-compose.demo.yml down
}
