# Three Revert Watch Agent Notes

These instructions apply to the whole repository.

## Project Shape

Three Revert Watch monitors Wikipedia edit/revert activity and exposes a public
dashboard for conflict-topic activity.

Main pipeline:

```text
Collector -> TopicMatcher -> ConflictDetector -> Aggregator -> Gateway -> Frontend
```

Projects:

- `ThreeRevertWatch.Contracts`: shared DTOs and event contracts.
- `ThreeRevertWatch.Infrastructure`: Kafka, Postgres schema/migrations, logging,
  and shared options.
- `ThreeRevertWatch.Collector`: raw Wikipedia recent-change ingestion.
- `ThreeRevertWatch.TopicMatcher`: conflict topic matching and Wikipedia
  category cache logic.
- `ThreeRevertWatch.ConflictDetector`: edit classification, revert graph, and
  article conflict scoring.
- `ThreeRevertWatch.Aggregator`: topic/article read model API.
- `ThreeRevertWatch.Gateway`: public REST proxy and SignalR hub.
- `ThreeRevertWatch.Frontend`: Blazor dashboard.
- `ThreeRevertWatch.Tests`: unit tests.

## Current Public Demo

The active demo is hosted on a single VPS:

```text
Public URL: https://threerevertwatch.ru
Fallback URL: https://195-209-213-190.sslip.io
VPS: 195.209.213.190
SSH user: ubuntu
Deploy path: /opt/ThreeRevertWatch
Domain DNS: A @ and A www -> 195.209.213.190
```

The demo stack uses:

- Docker Compose.
- Caddy for reverse proxy and automatic HTTPS.
- Postgres, Redis, Kafka, Zookeeper.
- Short retention suitable for a small public demo.

Do not commit private keys, `.env`, generated passwords, server-only secrets, or
downloaded key files. Keep credentials in local files or GitHub Actions secrets.

## Required Toolchain

The solution uses the .NET SDK from `global.json`:

```text
.NET SDK 10.0.202, rollForward latestFeature
```

Useful local commands:

```powershell
dotnet restore ThreeRevertWatch.slnx
dotnet build ThreeRevertWatch.slnx
dotnet test ThreeRevertWatch.slnx
dotnet test ThreeRevertWatch.slnx --configuration Release --no-build --verbosity minimal
```

If PowerShell script execution is blocked locally, run scripts with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 -BaseUrl https://threerevertwatch.ru
```

## Local And LAN Run

Create `.env` from `.env.example` and set at least `POSTGRES_PASSWORD`.

LAN helper:

```powershell
.\scripts\up-lan.ps1
.\scripts\logs.ps1
.\scripts\smoke-test.ps1 -LanHost <LAN_HOST> -Port 8080
.\scripts\down-lan.ps1
```

Manual LAN compose command:

```powershell
docker compose -f docker-compose.yml -f docker-compose.lan.yml up --build
```

LAN endpoint:

```text
http://<LAN_HOST>:8080
```

## Public Demo Run

Public demo config lives in:

```text
docker-compose.demo.yml
caddy/Caddyfile
scripts/deploy-demo.sh
scripts/demo-retention.sql
docs/DEMO_DEPLOY.md
docs/CI_CD.md
```

Demo start:

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build
```

Demo checks:

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml ps
curl -fsS https://threerevertwatch.ru/health
curl -fsS https://threerevertwatch.ru/api/conflicts/topics
```

PowerShell smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 -BaseUrl https://threerevertwatch.ru
```

Demo stop:

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml down
```

Only delete volumes deliberately:

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml down -v
```

`down -v` deletes persisted Postgres/Kafka/Redis data.

## Deployment Workflow

Normal deployment on the VPS:

```bash
cd /opt/ThreeRevertWatch
DEPLOY_BRANCH=master bash scripts/deploy-demo.sh
```

From local Windows using SSH:

```powershell
$keyPath = "<path-to-private-key-outside-repo>"
ssh -i $keyPath ubuntu@195.209.213.190 "cd /opt/ThreeRevertWatch && DEPLOY_BRANCH=master bash scripts/deploy-demo.sh"
```

For config-only changes to Caddy/Compose, a lighter remote update can be enough:

```bash
cd /opt/ThreeRevertWatch
git fetch origin master
git pull --ff-only origin master
docker compose -f docker-compose.yml -f docker-compose.demo.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --no-build --force-recreate gateway reverse-proxy
```

Use the full `scripts/deploy-demo.sh` path when application code or Docker image
inputs changed.

## CI/CD

GitHub Actions:

- `.github/workflows/ci.yml`: restore, build, test, and demo compose validation.
- `.github/workflows/deploy-demo.yml`: optional SSH deploy to the VPS.

Required GitHub Actions secrets for deploy:

```text
DEMO_SSH_HOST
DEMO_SSH_USER
DEMO_SSH_KEY
DEMO_DEPLOY_PATH
```

Optional:

```text
DEMO_SSH_PORT
DEMO_PUBLIC_URL=https://threerevertwatch.ru
DEMO_AUTO_DEPLOY=true
```

If `DEMO_AUTO_DEPLOY` is not `true`, deploy manually from the `Deploy demo`
workflow in GitHub Actions.

## Operational Guardrails

The public VPS is intentionally small. Keep changes conservative:

- Avoid adding always-on heavy services.
- Keep Kafka retention and Postgres cleanup enabled.
- Keep Docker log rotation enabled.
- Keep Postgres, Redis, Kafka, and Seq private.
- Keep Caddy as the only public HTTP/HTTPS entry point.
- Keep `www.threerevertwatch.ru` redirecting to `threerevertwatch.ru`.
- Preserve `PUBLIC_FALLBACK_HOST` support for fresh DNS or incident fallback.

Current demo retention defaults are short by design:

```text
DEMO_EDIT_RETENTION_DAYS=3
DEMO_SNAPSHOT_RETENTION_DAYS=5
DEMO_CANDIDATE_RETENTION_DAYS=5
KAFKA_LOG_RETENTION_HOURS=24
```

Before increasing retention, check disk usage:

```bash
df -h /
docker system df
```

Before assuming the app is broken, check DNS and Caddy certificate state:

```bash
dig @8.8.8.8 threerevertwatch.ru A +short
docker compose -f docker-compose.yml -f docker-compose.demo.yml logs --tail=120 reverse-proxy
```

## Database And Migrations

The demo overlay has a `db-init` service that runs
`db/migrations/001_conflict_monitoring.sql` once the database is healthy.

In demo mode:

- `Aggregator` and `ConflictDetector` set `Database__MigrateOnStartup=false`.
- `TopicMatcher` still seeds/migrates its topic data as part of startup.
- `db-init` serializes the shared schema initialization to avoid startup races.

When changing schema:

1. Update migration SQL and any related infrastructure code.
2. Make migration idempotent where possible.
3. Run tests.
4. Validate demo compose config.
5. Deploy carefully; do not delete volumes unless the user explicitly wants a
   fresh demo database.

## Verification Checklist

Before pushing substantial code changes:

```powershell
dotnet restore ThreeRevertWatch.slnx
dotnet build ThreeRevertWatch.slnx --configuration Release --no-restore
dotnet test ThreeRevertWatch.slnx --configuration Release --no-build --verbosity minimal
```

Validate demo compose:

```powershell
docker compose -f docker-compose.yml -f docker-compose.demo.yml config --quiet
```

If local Docker is unavailable or points at an unreachable Docker host, validate
on the VPS instead:

```bash
cd /opt/ThreeRevertWatch
docker compose -f docker-compose.yml -f docker-compose.demo.yml config --quiet
```

After deploy:

```powershell
curl.exe -I https://threerevertwatch.ru/health
curl.exe https://threerevertwatch.ru/api/conflicts/topics
curl.exe -I https://www.threerevertwatch.ru/health
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 -BaseUrl https://threerevertwatch.ru
```

Expected:

- `/health` returns `200`.
- `/api/conflicts/topics` returns JSON topic data.
- `www` redirects to the root domain.
- SignalR negotiate succeeds.

## Coding Guidance

- Prefer the existing project boundaries over adding new cross-cutting helpers.
- Put shared contracts in `ThreeRevertWatch.Contracts`.
- Put shared Kafka/Postgres/options/logging behavior in
  `ThreeRevertWatch.Infrastructure`.
- Keep public API routes stable unless the frontend and smoke checks are updated
  together.
- Keep event/DTO changes backward-aware across Collector, TopicMatcher,
  ConflictDetector, Aggregator, Gateway, and Frontend.
- Add focused tests in `ThreeRevertWatch.Tests` for scoring, matching,
  classification, retention, and route-contract changes.
- Do not refactor deployment files casually; they are tuned for a very small
  VPS.

## Git Notes

The primary branch is `master`.

Before starting:

```powershell
git status --short --branch
git pull --ff-only origin master
```

After changes:

```powershell
git status --short
git diff --check
git add <files>
git commit -m "<concise message>"
git push origin master
```

Do not rewrite public history or force-push unless the user explicitly asks.
