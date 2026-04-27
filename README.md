# Three Revert Watch

Realtime monitoring for Wikipedia edit wars and conflict topics.

Three Revert Watch replaces the old trend/anomaly pipeline with conflict-centered state:

```text
Collector -> TopicMatcher -> ConflictDetector -> Aggregator -> Gateway -> Frontend
```

## Projects

- `ThreeRevertWatch.Contracts` shared DTOs/events
- `ThreeRevertWatch.Infrastructure` Kafka, Postgres schema, logging, options
- `ThreeRevertWatch.Collector` raw Wikipedia edits
- `ThreeRevertWatch.TopicMatcher` rule/manual conflict topic membership
- `ThreeRevertWatch.ConflictDetector` edit classification, revert graph, article score
- `ThreeRevertWatch.Aggregator` topic/article read model API
- `ThreeRevertWatch.Gateway` REST proxy and SignalR hub
- `ThreeRevertWatch.Frontend` Blazor dashboard
- `ThreeRevertWatch.Tests` unit tests

## API

- `GET /api/conflicts/topics`
- `GET /api/conflicts/topics/{topicId}`
- `GET /api/conflicts/topics/{topicId}/articles`
- `GET /api/conflicts/topics/{topicId}/articles/{pageId}`
- `GET /api/conflicts/topics/{topicId}/articles/{pageId}/edits`
- `GET /api/conflicts/topics/{topicId}/articles/{pageId}/participants`
- SignalR hub: `/hubs/conflicts`

## Local Build

```powershell
dotnet build ThreeRevertWatch.slnx
dotnet test ThreeRevertWatch.slnx
```

## LAN Production-Like Run

1. Copy `.env.example` to `.env`.
2. Set `LAN_HOST` to the IPv4 address of this machine on your local network.
3. Change `POSTGRES_PASSWORD`.
4. Start the stack:

```powershell
docker compose -f docker-compose.yml -f docker-compose.lan.yml up --build
```

On hosts with the legacy Compose binary, use `docker-compose` with the same arguments.

The public LAN endpoint is:

```text
http://<LAN_HOST>:8080
```

The reverse proxy serves:

- UI at `/`
- Gateway API at `/api/conflicts/...`
- SignalR at `/hubs/conflicts`

Kafka, Postgres, Redis, Aggregator, TopicMatcher, ConflictDetector, and Collector are internal-only in LAN mode. `Seq` is optional under the `debug` profile.

## Find LAN IP On Windows

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
  Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' }
```

## Smoke Checks

```powershell
.\scripts\smoke-test.ps1 -LanHost <LAN_HOST> -Port 8080
```

Manual checks:

- `http://<LAN_HOST>:8080/health`
- `http://<LAN_HOST>:8080/api/conflicts/topics`
- open `http://<LAN_HOST>:8080` from another LAN device
- check SignalR negotiate:

```powershell
Invoke-RestMethod -Method Post "http://<LAN_HOST>:8080/hubs/conflicts/negotiate?negotiateVersion=1"
```

## Logs

```powershell
.\scripts\logs.ps1
.\scripts\logs.ps1 gateway
```

Optional Seq:

```powershell
docker compose --profile debug -f docker-compose.yml -f docker-compose.lan.yml up --build
```

In debug profile, expose Seq only deliberately, for example by adding a local port binding in `docker-compose.override.yml`.

## Stop / Clean

```powershell
.\scripts\down-lan.ps1
docker compose -f docker-compose.yml -f docker-compose.lan.yml down -v
```

The second command deletes persistent volumes.

## Current TODO

- Partial revert detection has DTO support but only explicit/exact revert MVP logic.
- Disputed fragment extraction is not deeply implemented yet.
- Manual review tooling for candidate topic articles is not implemented yet.
