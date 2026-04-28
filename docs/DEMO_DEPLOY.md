# Public Demo Deploy

This is the short path for a public demo that can live for a few weeks.
It keeps the current single-host Docker Compose architecture and adds:

- Caddy reverse proxy with automatic HTTPS for a real domain.
- Kafka log retention defaults for a small demo box.
- A Postgres cleanup sidecar that keeps detailed history short.

## 1. Point A Domain At The Server

Create a DNS `A` record:

```text
demo.example.com -> <server-public-ip>
```

Open inbound TCP ports `80` and `443` on the VM firewall/security group.
Keep Postgres, Redis, Kafka, and Seq private.

If the domain is managed in Cloudflare and this stack uses Caddy for HTTPS,
start with the record in DNS-only mode so Let's Encrypt can reach the VM
directly. A Cloudflare Tunnel is a separate option if you prefer not to expose
ports on the VM.

## 2. Configure `.env`

Copy `.env.example` to `.env` and set at least:

```env
PUBLIC_HOST=demo.example.com
PUBLIC_HTTP_PORT=80
PUBLIC_HTTPS_PORT=443
POSTGRES_PASSWORD=replace-with-a-strong-password
KAFKA_TOPIC_PARTITIONS=3
DEMO_EDIT_RETENTION_DAYS=3
DEMO_SNAPSHOT_RETENTION_DAYS=7
DEMO_CANDIDATE_RETENTION_DAYS=7
DEMO_CLEANUP_INTERVAL_SECONDS=86400
```

`PUBLIC_HOST` must be only the host name, without `https://` and without a path.

## 3. Start The Demo Stack

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build
```

Caddy obtains and renews the TLS certificate automatically once DNS points to
the VM and ports `80`/`443` are reachable.

PowerShell helper:

```powershell
.\scripts\up-demo.ps1 -PublicHost demo.example.com
```

## 4. Check It

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml ps
curl -fsS https://demo.example.com/health
curl -fsS https://demo.example.com/api/conflicts/topics
```

PowerShell smoke test:

```powershell
.\scripts\smoke-test.ps1 -BaseUrl https://demo.example.com
```

The public URL is:

```text
https://demo.example.com
```

## 5. Logs And Stop

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml logs -f --tail=200
docker compose -f docker-compose.yml -f docker-compose.demo.yml down
```

PowerShell helpers:

```powershell
.\scripts\logs-demo.ps1
.\scripts\down-demo.ps1
```

To delete demo data too:

```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml down -v
```
