# CI/CD

This repository has a small GitHub Actions setup for solo demo hosting.

## CI

`.github/workflows/ci.yml` runs on pushes and pull requests to `master` and
`main`:

- restore
- release build
- tests
- demo compose config validation

## Demo Deploy

`.github/workflows/deploy-demo.yml` deploys the current repository to a single
VPS over SSH. It does not use a Docker registry. The VPS pulls the branch and
rebuilds the Docker Compose stack locally.

Required GitHub Actions secrets:

```text
DEMO_SSH_HOST       VPS host or IP
DEMO_SSH_USER       SSH user on the VPS
DEMO_SSH_KEY        Private SSH key with access to the VPS
DEMO_DEPLOY_PATH    Repo path on the VPS, for example /opt/ThreeRevertWatch
```

Optional GitHub Actions secret:

```text
DEMO_SSH_PORT       SSH port, defaults to 22
```

Optional GitHub Actions variables:

```text
DEMO_PUBLIC_URL     https://demo.example.com
DEMO_AUTO_DEPLOY    true
```

If `DEMO_AUTO_DEPLOY` is not `true`, deployment is manual from GitHub Actions:
open `Deploy demo`, click `Run workflow`, and choose the branch. If it is
`true`, pushes to `master` or `main` also deploy automatically.

## First VPS Setup

On the VPS, install Git, Docker, and Docker Compose. Then clone the repository
once:

```bash
sudo mkdir -p /opt/ThreeRevertWatch
sudo chown "$USER:$USER" /opt/ThreeRevertWatch
git clone https://github.com/AlexeevLeonid/ThreeRevertWatch.git /opt/ThreeRevertWatch
cd /opt/ThreeRevertWatch
cp .env.example .env
```

Edit `.env` with the real `PUBLIC_HOST` and `POSTGRES_PASSWORD`.

For the first manual deploy from the VPS:

```bash
bash scripts/deploy-demo.sh
```

After that, GitHub Actions can run the same script remotely.
