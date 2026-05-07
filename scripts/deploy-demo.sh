#!/usr/bin/env bash
set -Eeuo pipefail

DEPLOY_BRANCH="${DEPLOY_BRANCH:-master}"

if [ ! -f ".env" ]; then
  echo "Missing .env in $(pwd). Create it from .env.example before first deploy." >&2
  exit 1
fi

if docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
else
  echo "Docker Compose is not installed." >&2
  exit 1
fi

# Compose v5 can route multi-service builds through buildx bake. On the small
# demo VPS that path can hang with no useful progress, while the ordinary
# compose builder remains reliable for this stack.
export COMPOSE_BAKE="${COMPOSE_BAKE:-false}"

echo "Deploying branch ${DEPLOY_BRANCH} in $(pwd)"
git fetch --prune origin
if git show-ref --verify --quiet "refs/heads/${DEPLOY_BRANCH}"; then
  git checkout "$DEPLOY_BRANCH"
else
  git checkout -B "$DEPLOY_BRANCH" "origin/${DEPLOY_BRANCH}"
fi
git pull --ff-only origin "$DEPLOY_BRANCH"

"${COMPOSE[@]}" -f docker-compose.yml -f docker-compose.demo.yml config --quiet

BUILD_SERVICES=(
  reverse-proxy
  collector
  topicmatcher
  conflictdetector
  aggregator
  gateway
  frontend
)

for service in "${BUILD_SERVICES[@]}"; do
  "${COMPOSE[@]}" -f docker-compose.yml -f docker-compose.demo.yml build "$service"
done

if ! "${COMPOSE[@]}" -f docker-compose.yml -f docker-compose.demo.yml up -d --remove-orphans; then
  echo "Full compose up failed; retrying application services without dependency gate." >&2
  "${COMPOSE[@]}" -f docker-compose.yml -f docker-compose.demo.yml up -d --no-build --no-deps \
    aggregator collector topicmatcher conflictdetector gateway frontend reverse-proxy
fi
"${COMPOSE[@]}" -f docker-compose.yml -f docker-compose.demo.yml ps

docker builder prune -af >/dev/null || true
docker image prune -f >/dev/null || true
docker system df || true
