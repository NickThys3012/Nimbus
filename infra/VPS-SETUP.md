# Nimbus VPS setup runbook

Use this runbook to turn a blank Ubuntu 24.04 Contabo VPS (or equivalent single-host VM) into the
current Nimbus production environment. Follow it top-to-bottom once; after that, day-to-day deploys
happen through GitHub Actions.

Conventions used throughout:

- `[LOCAL]` = your Mac or workstation.
- `[VPS]` = SSH session as `deploy`.
- `[VPS as root]` = initial root login or Contabo VNC/console.
- `[GITHUB]` = GitHub repository/environment settings.

Replace `<vps-ip>` and `<domain>` everywhere.

## Part A: Verify the repo-managed deployment files

Nothing in this part changes the VPS. It is the checklist of files that define the final production
state.

### A1. Required files [LOCAL]

From the repo root, confirm the deployment payload exists:

```bash
cd ~/path/to/Nimbus
ls   infra/compose/docker-compose.prod.yml   infra/compose/docker-compose.limits.yml   infra/caddy/Caddyfile   infra/observability/prometheus.yml   infra/observability/alert.rules.yml   infra/observability/loki-config.yaml   infra/docker/daemon.json   infra/compose/.env.example   infra/minio/minio-init.sh   infra/db/sqlserver-init.sh   infra/db/sqlserver-init.sql
ls infra/observability/grafana
ls infra/scripts
```

Required directories under `infra/observability/grafana/` are `provisioning/` and `dashboards/`.

### A2. What to check before you copy anything [LOCAL]

- `infra/compose/docker-compose.prod.yml` is the canonical stack file. On the VPS it is copied as
  `/opt/nimbus/compose.yaml`.
- `infra/compose/docker-compose.limits.yml` is the resource-limits override. On the VPS it is copied as
  `/opt/nimbus/compose.override.yaml` so Compose auto-loads it alongside `compose.yaml`.
- `infra/caddy/Caddyfile` must route exactly three hostnames: `nimbus.`, `grafana.`, and `console.`.
- `infra/compose/.env.example` is the source of truth for the full variable list; do not invent extra keys.
- `infra/minio/minio-init.sh` and `infra/db/sqlserver-init.*` are intentionally idempotent one-shot bootstrap
  jobs; do not replace them with manual console/database clicking.
- `infra/compose/docker-compose.prod.yml` should still be using bind mounts under `/srv/nimbus/data/*`.
  That is deliberate: `docker compose down` does not touch them, and path-based backups can capture
  them directly.
- `sqlserver`'s healthcheck should still have a `start_period: 30s`. SQL Server cold start is slow;
  shortening that creates false negatives during a clean bring-up.
- Only `caddy` should publish ports. Docker's iptables rules are evaluated before `ufw`, so a new
  `ports:` entry is a real internet exposure even if the host firewall says otherwise.

### A3. Note on profiles [LOCAL]

The file still contains both profiles:

- `--profile app` = normal production.
- `--profile stub` = infra-only fallback using `api-stub`.

For a real production VPS, use `--profile app`. Keep `stub` only for troubleshooting or GHCR outage
scenarios.

## Part B: Create the DNS records

### B1. Public records [LOCAL / DNS]

Create these `A` records before Caddy ever starts:

```text
nimbus.<domain>   A   <vps-ip>
grafana.<domain>  A   <vps-ip>
console.<domain>  A   <vps-ip>
```

Then verify propagation:

```bash
dig +short nimbus.<domain>
dig +short grafana.<domain>
dig +short console.<domain>
```

All three must resolve to the VPS IP before Part E.

## Part C: Build the host baseline

### C1. Create the deploy user [VPS as root]

Get the public key you will use for day-to-day admin access:

```bash
# [LOCAL]
cat ~/.ssh/id_ed25519.pub
```

Then create `deploy`, install the key, and allow passwordless `sudo`:

```bash
# [VPS as root]
adduser --disabled-password --gecos "" deploy
usermod -aG sudo deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh

cat > /home/deploy/.ssh/authorized_keys <<'EOF'
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI...paste-your-whole-key-line-here... nickt@Mac
EOF

chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys
chmod 750 /home/deploy

echo 'deploy ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/90-deploy
chmod 440 /etc/sudoers.d/90-deploy
visudo -c
```

`NOPASSWD` is required because `deploy` has no password to answer a `sudo` prompt with.

### C2. Gate: prove fresh SSH and passwordless sudo [LOCAL]

Do not harden SSH until this succeeds from a new terminal:

```bash
ssh deploy@<vps-ip> 'id && sudo -n true && echo GATE_PASSED'
```

Keep the original root session open until the new `deploy` session works.

### C3. Harden SSH [VPS]

```bash
sudo tee /etc/ssh/sshd_config.d/10-nimbus.conf > /dev/null <<'EOF'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
ChallengeResponseAuthentication no
PubkeyAuthentication yes
AllowUsers deploy
X11Forwarding no
AllowAgentForwarding no
MaxAuthTries 3
LoginGraceTime 30
ClientAliveInterval 300
ClientAliveCountMax 2
EOF

sudo chmod 644 /etc/ssh/sshd_config.d/10-nimbus.conf
sudo rm -f /etc/ssh/sshd_config.d/50-cloud-init.conf
echo 'ssh_pwauth: false' | sudo tee /etc/cloud/cloud.cfg.d/99-nimbus-disable-ssh.cfg

sudo sshd -t && sudo systemctl restart ssh
sudo sshd -T | grep -Ei 'allowusers|permitrootlogin|passwordauthentication|kbdinteractive'
```

Expected effective values:

```text
allowusers deploy
permitrootlogin no
passwordauthentication no
kbdinteractiveauthentication no
```

Now verify from another new terminal:

```bash
ssh deploy@<vps-ip> 'echo OK'   # must succeed
ssh root@<vps-ip>               # must be refused
```

### C4. Enable UFW and fail2ban [VPS]

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable

sudo apt-get update
sudo apt-get install -y fail2ban

sudo tee /etc/fail2ban/jail.local > /dev/null <<'EOF'
[DEFAULT]
backend  = systemd
bantime  = 1h
findtime = 10m
maxretry = 5
ignoreip = 127.0.0.1/8 ::1

[sshd]
enabled  = true
port     = 22
maxretry = 3
bantime  = 24h
EOF

sudo systemctl enable --now fail2ban
sudo ufw status verbose
sudo fail2ban-client status sshd
```

Keep port 80 open: Let's Encrypt's HTTP-01 challenge needs it.

### C5. Install Docker and set the daemon policy [VPS]

```bash
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable"   | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Copy the repo-managed daemon config onto the host and apply it:

```bash
# [LOCAL]
scp infra/docker/daemon.json deploy@<vps-ip>:~/daemon.json
```

```bash
# [VPS]
sudo cp ~/daemon.json /etc/docker/daemon.json
sudo systemctl restart docker
sudo systemctl enable docker
sudo usermod -aG docker deploy
```

Log out and back in once so `docker` works without `sudo`.

The daemon-wide json-file rotation caps are deliberate: container stdout goes to Docker logs even
when the application also ships structured logs to Loki.

### C6. Add swap and host sysctls [VPS]

```bash
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

sudo tee /etc/sysctl.d/99-nimbus.conf > /dev/null <<'EOF'
vm.swappiness = 10
vm.overcommit_memory = 0
net.ipv4.tcp_syncookies = 1
EOF

sudo sysctl --system
free -h && swapon --show && sysctl vm.swappiness
```

The real memory guardrails are the container limits in `compose.override.yaml`; low swappiness just
keeps swap as a cushion instead of a working tier.

### C7. Create the persistent data directories [VPS]

Create the bind-mount sources before any `docker compose up -d`:

```bash
sudo install -d -m 755 /srv/nimbus/data
sudo install -d -m 770 -o 10001 -g 0     /srv/nimbus/data/mssql
sudo install -d -m 750 -o 1000  -g 1000  /srv/nimbus/data/minio
sudo install -d -m 750 -o 10001 -g 10001 /srv/nimbus/data/loki
sudo install -d -m 750 -o 65534 -g 65534 /srv/nimbus/data/prometheus
sudo install -d -m 750 -o 472   -g 0     /srv/nimbus/data/grafana
sudo install -d -m 700 -o 0     -g 0     /srv/nimbus/data/caddy
sudo install -d -m 700 -o 0     -g 0     /srv/nimbus/data/caddy/data
sudo install -d -m 700 -o 0     -g 0     /srv/nimbus/data/caddy/config
sudo install -d -m 755 /var/lib/node_exporter/textfile_collector
ls -ln /srv/nimbus/data
```

Why these exact owners matter:

- SQL Server writes as container UID `10001` with group `0`, so `/srv/nimbus/data/mssql` must be
  `10001:0` and group-writable (`770`).
- MinIO writes as `1000:1000`.
- Grafana and Loki also need writable host directories owned by the UIDs their images run as.
- If the source directory is missing, Docker creates it as `root:root`; that is the most common
  cause of first-run restart loops on a fresh VPS.

### C8. Enable unattended upgrades and create /opt/nimbus [VPS]

```bash
sudo apt-get install -y unattended-upgrades

sudo tee /etc/apt/apt.conf.d/51-nimbus-unattended > /dev/null <<'EOF'
Unattended-Upgrade::Allowed-Origins {
    "${distro_id}:${distro_codename}-security";
    "${distro_id}ESMApps:${distro_codename}-apps-security";
    "${distro_id}ESM:${distro_codename}-infra-security";
};
Unattended-Upgrade::Automatic-Reboot "false";
Unattended-Upgrade::Remove-Unused-Kernel-Packages "true";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
EOF

sudo tee /etc/apt/apt.conf.d/20auto-upgrades > /dev/null <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF

sudo systemctl enable --now unattended-upgrades
sudo install -d -m 750 -o deploy -g deploy /opt/nimbus
stat -c '%a %U:%G %n' /opt/nimbus
```

Expected ownership: `750 deploy:deploy /opt/nimbus`.

## Part D: Populate /opt/nimbus/.env

Always start from `infra/compose/.env.example`. Keep every key present even if a later subsystem is not yet
active in your environment.

### D1. Generate the secrets [VPS]

Generate and record these before you paste anything into `.env`:

```bash
echo "MSSQL_SA_PASSWORD=$(openssl rand -base64 24)"
echo "MSSQL_APP_PASSWORD=$(openssl rand -base64 24)"
echo "MSSQL_MIGRATOR_PASSWORD=$(openssl rand -base64 24)"
echo "MINIO_ROOT_PASSWORD=$(openssl rand -base64 24)"
echo "MINIO_APP_SECRET_KEY=$(openssl rand -base64 24)"
echo "MINIO_CONSOLE_PASSWORD=$(openssl rand -base64 18)"
echo "GRAFANA_ADMIN_PASSWORD=$(openssl rand -base64 24)"
docker run --rm caddy:2-alpine caddy hash-password --plaintext '<paste MINIO_CONSOLE_PASSWORD>'
```

Notes:

- SQL Server rejects weak passwords. If a generated password lacks a digit, generate another.
- When you paste `MINIO_CONSOLE_PASSWORD_HASH` into `.env`, escape every `$` as `$$`.
- Put every real secret in your password manager while you have it on screen.

### D2. Write `/opt/nimbus/.env` [VPS]

```bash
cat > /opt/nimbus/.env <<'EOF'
NIMBUS_DOMAIN=example.be
ACME_EMAIL=you@example.be
ASPNETCORE_ENVIRONMENT=Production
IMAGE_TAG=latest

MSSQL_SA_PASSWORD=<paste>
MSSQL_PID=Express
MSSQL_MEMORY_LIMIT_MB=1792
MSSQL_APP_PASSWORD=<paste>
MSSQL_MIGRATOR_PASSWORD=<paste>

MINIO_ROOT_USER=nimbus
MINIO_ROOT_PASSWORD=<paste>
MINIO_APP_ACCESS_KEY=nimbus-app
MINIO_APP_SECRET_KEY=<paste>
MINIO_CONSOLE_USER=<pick-a-console-login>
MINIO_CONSOLE_PASSWORD_HASH=<paste-the-bcrypt-hash-with-every-$-doubled>

GRAFANA_ADMIN_USER=<pick-an-admin-login>
GRAFANA_ADMIN_PASSWORD=<paste>

Loki__Url=http://loki:3100

RESTIC_REPOSITORY=
RESTIC_PASSWORD=

EMAIL_PROVIDER_API_KEY=
EMAIL_FROM=
EOF

chmod 600 /opt/nimbus/.env
stat -c '%a %U:%G' /opt/nimbus/.env
```

Keep the key set aligned with `infra/compose/.env.example`. `RESTIC_*` and email values are included here so
rebuilds do not depend on memory; if those integrations are not active yet in your environment, keep
those keys present and intentionally blank until you wire them up.

### D3. Validate `.env` interpolation [VPS]

After Part E copies the compose files into `/opt/nimbus`, run:

```bash
cd /opt/nimbus
docker compose --profile app config > /dev/null && echo "CONFIG OK"
docker compose --profile app config | grep -E 'MINIO_CONSOLE_PASSWORD_HASH|MSSQL_|MINIO_|GRAFANA_'
```

If the console hash was pasted correctly, `docker compose config` shows the `$$`-escaped form, while
inside the container Caddy will see the original bcrypt hash.

## Part E: First deploy

### E1. Copy the repo-managed files to the VPS [LOCAL]

```bash
cd ~/path/to/Nimbus
scp infra/compose/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/compose/docker-compose.limits.yml deploy@<vps-ip>:/opt/nimbus/compose.override.yaml
scp infra/caddy/Caddyfile infra/observability/prometheus.yml infra/observability/alert.rules.yml infra/observability/loki-config.yaml     infra/minio/minio-init.sh infra/db/sqlserver-init.sh infra/db/sqlserver-init.sql     deploy@<vps-ip>:/opt/nimbus/
scp -r infra/observability/grafana deploy@<vps-ip>:/opt/nimbus/
scp -r infra/scripts deploy@<vps-ip>:~/nimbus-scripts/
ssh deploy@<vps-ip> 'ls -la /opt/nimbus && ls -la ~/nimbus-scripts'
```

`compose.override.yaml` is the deployed name for `infra/compose/docker-compose.limits.yml`; that naming keeps
resource limits automatically layered into every `docker compose` command on the VPS.

### E2. Configure GHCR pull access [VPS]

The `api` and `migrator` images are pulled from GHCR. Store a PAT with `read:packages` in the
`deploy` user's Docker credential store:

```bash
mkdir -p ~/.docker
echo '<PAT with read:packages>' | docker login ghcr.io -u NickThys3012 --password-stdin
cat ~/.docker/config.json
```

You should see an `auths.ghcr.io` entry. This token stays on the VPS; the GitHub Actions workflow
never reads it.

### E3. Install the host-side observability scripts and timers [VPS]

Install the repo-managed scripts plus the directory-size exporter from the original base runbook:

```bash
sudo install -m 755 ~/nimbus-scripts/nimbus-cert-expiry.sh /usr/local/bin/
sudo install -m 755 ~/nimbus-scripts/nimbus-container-restarts.sh /usr/local/bin/
sudo install -m 755 ~/nimbus-scripts/nimbus-mssql-size.sh /usr/local/bin/

sudo tee /usr/local/bin/nimbus-dirsize.sh > /dev/null <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
OUT=/var/lib/node_exporter/textfile_collector/nimbus_dirsize.prom
TMP="${OUT}.$$"
{
  echo '# HELP nimbus_directory_size_bytes Size of a Nimbus data directory.'
  echo '# TYPE nimbus_directory_size_bytes gauge'
  for d in /srv/nimbus/data/*/; do
    name=$(basename "$d")
    size=$(du -sb "$d" | cut -f1)
    echo "nimbus_directory_size_bytes{directory="${name}"} ${size}"
  done
} > "$TMP"
mv "$TMP" "$OUT"
EOF
sudo chmod 755 /usr/local/bin/nimbus-dirsize.sh
```

Create the systemd units:

```bash
sudo tee /etc/systemd/system/nimbus-dirsize.service > /dev/null <<'EOF'
[Unit]
Description=Export Nimbus data directory sizes as Prometheus metrics

[Service]
Type=oneshot
ExecStart=/usr/local/bin/nimbus-dirsize.sh
EOF

sudo tee /etc/systemd/system/nimbus-dirsize.timer > /dev/null <<'EOF'
[Unit]
Description=Run the Nimbus directory-size exporter every 15 minutes

[Timer]
OnBootSec=5min
OnUnitActiveSec=15min
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo tee /etc/systemd/system/nimbus-cert-expiry.service > /dev/null <<'EOF'
[Unit]
Description=Export Caddy certificate expiry as a Prometheus metric

[Service]
Type=oneshot
ExecStart=/usr/local/bin/nimbus-cert-expiry.sh
EOF

sudo tee /etc/systemd/system/nimbus-cert-expiry.timer > /dev/null <<'EOF'
[Unit]
Description=Run nimbus-cert-expiry hourly

[Timer]
OnCalendar=hourly
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo tee /etc/systemd/system/nimbus-container-restarts.service > /dev/null <<'EOF'
[Unit]
Description=Export Docker restart counts for Nimbus containers

[Service]
Type=oneshot
ExecStart=/usr/local/bin/nimbus-container-restarts.sh
EOF

sudo tee /etc/systemd/system/nimbus-container-restarts.timer > /dev/null <<'EOF'
[Unit]
Description=Run nimbus-container-restarts every 2 minutes

[Timer]
OnCalendar=*:0/2
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo tee /etc/systemd/system/nimbus-mssql-size.service > /dev/null <<'EOF'
[Unit]
Description=Export Nimbus SQL Server database size as a Prometheus metric

[Service]
Type=oneshot
EnvironmentFile=/opt/nimbus/.env
ExecStart=/usr/local/bin/nimbus-mssql-size.sh
EOF

sudo tee /etc/systemd/system/nimbus-mssql-size.timer > /dev/null <<'EOF'
[Unit]
Description=Run nimbus-mssql-size hourly

[Timer]
OnCalendar=hourly
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now   nimbus-dirsize.timer   nimbus-cert-expiry.timer   nimbus-container-restarts.timer   nimbus-mssql-size.timer
sudo /usr/local/bin/nimbus-dirsize.sh
cat /var/lib/node_exporter/textfile_collector/nimbus_dirsize.prom
```

`NimbusBackupStale` in `infra/observability/alert.rules.yml` is the contract for a future restic job. Until that job
exists and writes `nimbus_backup_last_success_timestamp`, that alert will remain expected noise.

### E4. Start the stack in dependency order [VPS]

First validate the merged config exactly as the host will use it:

```bash
cd /opt/nimbus
docker compose --profile app config > /dev/null && echo "CONFIG OK"
```

Then bring services up in this order:

```bash
cd /opt/nimbus

# 1. Database
docker compose --profile app up -d sqlserver
docker compose --profile app ps sqlserver
docker compose --profile app exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"

# 2. Least-privilege SQL bootstrap
docker compose --profile app up sqlserver-init
docker compose --profile app logs sqlserver-init --tail 50

# 3. Object store
docker compose --profile app up -d minio
docker compose --profile app ps minio
docker compose --profile app exec -T minio mc ready local

# 4. MinIO bucket/policy/app-user bootstrap
docker compose --profile app up minio-init
docker compose --profile app logs minio-init --tail 50

# 5. Schema migrations
docker compose --profile app up --exit-code-from migrator migrator
docker compose --profile app logs migrator --tail 50

# 6. API
docker compose --profile app up -d api
docker compose --profile app exec -T api curl -fsS http://localhost:8080/health/ready

# 7. Reverse proxy / TLS
docker compose --profile app up -d caddy
docker compose --profile app logs caddy --tail 100

# 8. Observability
docker compose --profile app up -d loki prometheus node-exporter grafana
docker compose --profile app ps
```

Why this ordering exists:

- `sqlserver-init` creates the database and the least-privilege `nimbus_app` / `nimbus_migrator`
  logins exactly once and safely re-runs on later deploys.
- `migrator` is a one-shot container; it should exit successfully, not restart forever.
- `minio-init` creates the buckets, policy, and dedicated app user before the API tries to use them.
- Caddy is the only public surface, so the rest of the stack can be verified privately first.

### E5. Verify every service [VPS / LOCAL]

Run the service-level checks:

```bash
# [VPS]
cd /opt/nimbus
docker compose --profile app ps --format 'table {{.Service}}	{{.State}}'

docker compose --profile app exec -T api curl -fsS http://localhost:8080/health
docker compose --profile app exec -T api curl -fsS http://localhost:8080/health/ready
docker compose --profile app exec -T minio mc ready local
curl -sI https://nimbus.<domain> | head -1
curl -sI https://grafana.<domain> | head -1
curl -sI https://console.<domain> | head -1
curl -sI https://nimbus.<domain> | grep -Ei 'strict-transport-security|x-content-type-options|referrer-policy'

docker run --rm --network nimbus_nimbus curlimages/curl:latest   -s -o /dev/null -w '%{http_code}
' http://minio:9000/flight-images/
```

Expected results:

- `sqlserver` = running/healthy.
- `sqlserver-init` = exited `0` after logging `SQL Server bootstrap complete`.
- `migrator` = exited `0`.
- `minio` = running/healthy.
- `minio-init` = exited `0` after logging bucket/policy/app-user creation.
- `api` = running and `/health` + `/health/ready` return success.
- `caddy` = serving valid TLS for `nimbus.`, `grafana.`, and `console.`.
- `grafana` = login page reachable over HTTPS.
- `prometheus`, `loki`, `node-exporter` = running.
- Anonymous curl to MinIO returns `403`, not `200`.

Also verify what the host actually published:

```bash
# [VPS]
docker ps --format 'table {{.Names}}	{{.Ports}}'
sudo ss -tlnp | grep -vE '127\.|\[::1\]'
```

Only 22, 80, and 443 should be bound on `0.0.0.0` / `[::]`.

Finally, do the outside-in scan from a machine that is not the VPS:

```bash
# [LOCAL]
nmap -Pn -p1-65535 <vps-ip>
```

The only open TCP ports should be 22, 80, and 443.

## Part F: GitHub Actions CD setup

This is the one-time setup for `.github/workflows/cd.yml`.

### F1. Add the GitHub Actions secrets [GITHUB]

Repository settings → Secrets and variables → Actions → add:

| Secret | Value |
|---|---|
| `VPS_HOST` | VPS IP or hostname |
| `VPS_USER` | `deploy` |
| `VPS_SSH_KEY` | Private half of a dedicated deploy keypair |

Generate that dedicated keypair and install only its public half on the VPS:

```bash
# [LOCAL]
ssh-keygen -t ed25519 -f ./nimbus-deploy-key -C "github-actions-deploy" -N ""
ssh-copy-id -i ./nimbus-deploy-key.pub deploy@<vps-ip>
cat ./nimbus-deploy-key   # paste this into the VPS_SSH_KEY secret
rm ./nimbus-deploy-key ./nimbus-deploy-key.pub
```

Do not reuse your personal SSH key. No GHCR pull token is stored in GitHub: the workflow pushes with
its own `GITHUB_TOKEN`, while the VPS pulls using the `read:packages` token already stored in
`~/.docker/config.json` from Part E.

### F2. Create the `production` environment gate [GITHUB]

Repository settings → Environments → New environment → `production`.

Add at least one required reviewer. The deploy job in `cd.yml` is bound to this environment so a
merge to `main` builds and pushes images automatically, but the SSH deploy still waits for explicit
approval.

### F3. First automated deploy [GITHUB]

Merge any PR to `main`, then watch Actions:

1. `CI` completes on `main`.
2. `CD / build-and-push` builds and publishes `nimbus-api` and `nimbus-migrator` to GHCR tagged
   `latest` and the commit SHA.
3. `CD / deploy` waits for `production` approval.
4. After approval, the workflow SSHes to the VPS, pins `IMAGE_TAG` to the tested SHA, pulls images,
   runs `migrator`, swaps only `api`, polls `/health/ready`, and prunes dangling images.

### F4. Day-to-day workflow after that [GITHUB / VPS]

Normal release flow becomes:

- Merge reviewed PR to `main`.
- Approve the `production` deployment when ready.
- Confirm on the VPS:

```bash
# [VPS]
cd /opt/nimbus
grep '^IMAGE_TAG=' .env
docker compose --profile app ps
docker compose --profile app exec -T api curl -fsS http://localhost:8080/health/ready
docker image ls | grep nimbus-api
```

`IMAGE_TAG` should now be the deployed commit SHA, not `latest`.

### F5. Measure the swap window once [VPS]

This deploy is health-gated, not zero-downtime. Measure the interruption once during a real deploy:

```bash
while true; do
  code=$(curl -s -o /dev/null -w '%{http_code}' https://nimbus.<domain>/health)
  echo "$(date +%T) $code"
  sleep 0.5
done
```

Record the observed interruption in your deployment notes.

## Part G: Rollback

### G1. Image-only rollback [VPS]

Rollback means restoring the previous image tag and starting the API again:

```bash
cd /opt/nimbus
sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=<previous-sha>/" .env
docker compose --profile app pull api migrator
docker compose --profile app up -d api
docker compose --profile app exec -T api curl -fsS http://localhost:8080/health/ready
```

This does **not** roll back the database. Migrations therefore must stay additive and backward-
compatible with the immediately previous release; see [`../docs/architecture.md#deployment`](../docs/architecture.md#deployment).

## Part H: Ongoing operations

### H1. Resource budgets

See [`RESOURCE-BUDGET.md`](RESOURCE-BUDGET.md) for the current RAM/CPU ceilings, Prometheus/Loki
retention, disk budgets, and the load-test / reboot checks that still need a live-box exercise.

### H2. MinIO operations

See [`MINIO.md`](MINIO.md) for bucket policy, versioning, app-key scope, anonymous-access checks, and
restore-from-backup procedure.

### H3. External port scan and security verification

Re-run these whenever you change compose, Caddy, or firewall settings:

```bash
# [VPS]
sudo ufw status verbose
docker ps --format 'table {{.Names}}	{{.Ports}}'
sudo ss -tlnp | grep -vE '127\.|\[::1\]'

# [LOCAL]
nmap -Pn -p1-65535 <vps-ip>
```

The only public ports should remain 22, 80, and 443.

### H4. Break-glass recovery

If SSH access breaks, use the Contabo VNC/console and log in as `root`. Then inspect:

```bash
grep -r . /etc/ssh/sshd_config.d/
sshd -T | grep -Ei 'allowusers|permitrootlogin|passwordauthentication'
id deploy
ls -ld /home/deploy /home/deploy/.ssh; ls -l /home/deploy/.ssh/
ssh-keygen -lf /home/deploy/.ssh/authorized_keys
ls -l /etc/sudoers.d/; visudo -c
tail -40 /var/log/auth.log
```

Common fixes:

- Empty or wrong `authorized_keys` entry: rewrite it and restore `600` permissions.
- Broken `AllowUsers` / password-auth setting: fix `/etc/ssh/sshd_config.d/10-nimbus.conf`.
- Missing sudoers drop-in: recreate `/etc/sudoers.d/90-deploy`.
- Wrong `/opt/nimbus` ownership: `sudo chown -R deploy:deploy /opt/nimbus`.

Before you leave the console:

```bash
sshd -t && systemctl restart ssh
sshd -T | grep -Ei 'allowusers|permitrootlogin|passwordauthentication'
su - deploy -c 'sudo -n true && echo SUDO_OK'
```

Keep the console open until a real SSH login from your workstation succeeds.

### H5. Final verification checklist

Use this as the final blank-server acceptance pass:

- Ubuntu 24.04 installed.
- `deploy` exists, can SSH in, and has passwordless `sudo`.
- Root login and SSH password login are disabled.
- `ufw` exposes only 22/80/443; `fail2ban` protects `sshd`.
- Docker is from Docker's apt repo, log rotation is active, and `docker` works without `sudo`.
- Swap is active with `vm.swappiness=10`.
- `/srv/nimbus/data/*` exists with the expected numeric ownership.
- `/opt/nimbus` is `750 deploy:deploy`; `.env` is `600 deploy:deploy`.
- `compose.yaml` + `compose.override.yaml` parse cleanly with `docker compose --profile app config`.
- GHCR pull auth works from the VPS.
- `sqlserver`, `minio`, `api`, `caddy`, `loki`, `prometheus`, `node-exporter`, and `grafana` are
  running; `sqlserver-init`, `minio-init`, and `migrator` exit successfully.
- `https://nimbus.<domain>`, `https://grafana.<domain>`, and `https://console.<domain>` all work.
- Anonymous access to MinIO is denied.
- External `nmap` shows only 22, 80, and 443.
- You know the Contabo root password and can open the provider console if SSH ever breaks.
