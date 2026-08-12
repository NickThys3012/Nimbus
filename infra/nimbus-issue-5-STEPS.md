# Nimbus Issue #5 — Provision and harden the Contabo VPS

Linear instructions. Every file below is **complete and final** — you write each one exactly once and
never come back to edit it.

**Read before starting:**

* Every file is given in full. Copy the whole block; nothing is a fragment.
* No step modifies a file from an earlier step.
* Commands are marked `[LOCAL]` (your Mac) or `[VPS]` (over SSH).
* `[VPS]` commands assume you are logged in as `deploy` and include `sudo` where needed. The single
  exception is Part C1, which runs as `root`.
* Replace `<vps-ip>` with your VPS public IP and `<domain>` with your real domain throughout.

**If you have already done some of this**, skip to Part C0 and run the audit — it tells you exactly
which parts are done.

---

# PART A — Create the repo files `[LOCAL]`

Nothing runs in this part. You are authoring files and committing them.

## A1. Create the directory

```bash
cd ~/path/to/Nimbus
mkdir -p infra
```

## A2. `infra/docker-compose.prod.yml`

Complete file. The `api` / `migrator` / `api-stub` profiles are what let this file be final today
even though your registry is empty — see the note after the file.

```yaml
name: nimbus

services:
  # ---------------------------------------------------------------- front door
  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    environment:
      NIMBUS_DOMAIN: ${NIMBUS_DOMAIN}
      ACME_EMAIL: ${ACME_EMAIL}
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - /srv/nimbus/data/caddy/data:/data
      - /srv/nimbus/data/caddy/config:/config
    networks: [nimbus]

  # ------------------------------------------------- application (profile: app)
  api:
    profiles: ["app"]
    image: ghcr.io/nickthys3012/nimbus-api:${IMAGE_TAG:-latest}
    restart: unless-stopped
    expose: ["8080"]
    env_file: [.env]
    depends_on:
      migrator:
        condition: service_completed_successfully
    networks: [nimbus]

  migrator:
    profiles: ["app"]
    image: ghcr.io/nickthys3012/nimbus-migrator:${IMAGE_TAG:-latest}
    restart: on-failure
    env_file: [.env]
    depends_on:
      - sqlserver
    networks: [nimbus]

  # ------------------------------------------------ placeholder (profile: stub)
  api-stub:
    profiles: ["stub"]
    image: traefik/whoami:latest
    restart: unless-stopped
    command: ["--port", "8080"]
    networks:
      nimbus:
        aliases:
          - api
    # The alias means Caddy's "api:8080" resolves here while the real API
    # does not exist. Nothing in Caddyfile needs to change later.

  # ----------------------------------------------------------------- data tier
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD}
      MSSQL_PID: ${MSSQL_PID}
      MSSQL_MEMORY_LIMIT_MB: ${MSSQL_MEMORY_LIMIT_MB}
    volumes:
      - /srv/nimbus/data/mssql:/var/opt/mssql
    mem_limit: 5g
    networks: [nimbus]

  minio:
    image: minio/minio:latest
    restart: unless-stopped
    user: "1000:1000"
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - /srv/nimbus/data/minio:/data
    networks: [nimbus]

  # -------------------------------------------------------------- observability
  loki:
    image: grafana/loki:3.2.0
    restart: unless-stopped
    volumes:
      - /srv/nimbus/data/loki:/loki
    networks: [nimbus]

  prometheus:
    image: prom/prometheus:v2.55.0
    restart: unless-stopped
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./alert.rules.yml:/etc/prometheus/alert.rules.yml:ro
      - /srv/nimbus/data/prometheus:/prometheus
    networks: [nimbus]

  node-exporter:
    image: prom/node-exporter:v1.8.2
    restart: unless-stopped
    command:
      - '--path.rootfs=/host'
      - '--collector.textfile.directory=/host/var/lib/node_exporter/textfile_collector'
    volumes:
      - /:/host:ro,rslave
    networks: [nimbus]

  grafana:
    image: grafana/grafana:11.3.0
    restart: unless-stopped
    environment:
      GF_SERVER_ROOT_URL: https://grafana.${NIMBUS_DOMAIN}
      GF_SERVER_DOMAIN: grafana.${NIMBUS_DOMAIN}
      GF_SECURITY_ADMIN_USER: ${GRAFANA_ADMIN_USER}
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD}
      GF_USERS_ALLOW_SIGN_UP: "false"
      GF_AUTH_ANONYMOUS_ENABLED: "false"
      GF_SECURITY_COOKIE_SECURE: "true"
      GF_SECURITY_COOKIE_SAMESITE: strict
    volumes:
      - /srv/nimbus/data/grafana:/var/lib/grafana
    networks: [nimbus]

networks:
  nimbus:
    driver: bridge
```

**Two things to know about this file:**

**1. Only `caddy` has a `ports:` key.** Everything else is reachable only on the internal `nimbus`
network. This is deliberate and load-bearing — Docker publishes ports by writing NAT rules that
bypass ufw entirely, so `ports:` is the real firewall here, not `ufw`. If you ever add a second
`ports:` key, that needs a written reason.

**2. Profiles replace editing.** Services tagged `profiles: ["app"]` or `["stub"]` do not start
unless you name the profile:

| Command | What runs |
|---|---|
| `docker compose --profile stub up -d` | infrastructure + `api-stub` answering as `api` |
| `docker compose --profile app up -d` | infrastructure + real `api` and `migrator` |

Untagged services (caddy, sqlserver, minio, loki, prometheus, node-exporter, grafana) run in both.
You use `stub` today and `app` once CI pushes images. **No file changes between the two.**

> Image tags were current at time of writing. If a pull fails with `manifest unknown`, check the tag
> on Docker Hub — that is a tag problem, not a config problem.

## A3. `infra/Caddyfile`

Complete and final. This file never changes between the stub and the real API, because both answer on
`api:8080`.

```caddy
{
	email {$ACME_EMAIL}
}

nimbus.{$NIMBUS_DOMAIN} {
	reverse_proxy api:8080
}

grafana.{$NIMBUS_DOMAIN} {
	reverse_proxy grafana:3000
}
```

Caddy issues and renews TLS certificates on its own, sets `X-Forwarded-*` headers, and proxies
WebSockets without configuration. It routes on the `Host` header, so `https://<vps-ip>` matches no
site and returns a TLS error rather than your Grafana login — which is what you want.

## A4. `infra/prometheus.yml`

```yaml
global:
  scrape_interval: 30s
  evaluation_interval: 30s

rule_files:
  - /etc/prometheus/alert.rules.yml

scrape_configs:
  - job_name: prometheus
    static_configs:
      - targets: ["localhost:9090"]

  - job_name: node
    static_configs:
      - targets: ["node-exporter:9100"]

  - job_name: nimbus-api
    metrics_path: /metrics
    static_configs:
      - targets: ["api:8080"]
```

## A5. `infra/alert.rules.yml`

The `groups:` / `rules:` nesting is mandatory. Prometheus refuses to start on a malformed rules file
rather than skipping it, so a mistake here takes down the whole monitoring stack.

```yaml
groups:
  - name: nimbus-disk
    rules:
      - alert: NimbusDiskFillingUp
        expr: node_filesystem_avail_bytes{mountpoint="/"} < 20e9
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Less than 20 GB free on /"
          description: "{{ $value | humanize1024 }}B available."

      - alert: NimbusDirectoryGrowthAnomaly
        expr: predict_linear(nimbus_directory_size_bytes[6h], 7*24*3600) > 100e9
        for: 30m
        labels:
          severity: warning
        annotations:
          summary: "{{ $labels.directory }} on track to exceed 100 GB within a week"
```

> These fire in Prometheus but **notify nobody** until you configure Alertmanager or Grafana
> alerting. That is Sprint 0b. What matters now is that the file is valid and the metrics exist.

## A6. `infra/.env.example`

Committed with every key present and every value blank, so a rebuild tells you what is missing
instead of failing at runtime.

```bash
# Copy to /opt/nimbus/.env on the host and fill in. Never commit the filled version.

NIMBUS_DOMAIN=
ACME_EMAIL=
ASPNETCORE_ENVIRONMENT=Production
IMAGE_TAG=latest

MSSQL_SA_PASSWORD=
MSSQL_PID=Express
MSSQL_MEMORY_LIMIT_MB=4096

MINIO_ROOT_USER=
MINIO_ROOT_PASSWORD=
MINIO_BUCKET=nimbus

GRAFANA_ADMIN_USER=
GRAFANA_ADMIN_PASSWORD=

Loki__Url=http://loki:3100

RESTIC_REPOSITORY=
RESTIC_PASSWORD=

EMAIL_PROVIDER_API_KEY=
EMAIL_FROM=
```

## A7. Add to `.gitignore`

```bash
echo 'infra/.env' >> .gitignore
```

## A8. Self-review

Three checks. All three must pass before you commit.

```bash
# 1. Exactly one ports: key, and it must be caddy's
awk '/^  [a-z0-9_-]+:/ {svc=$1} /^[[:space:]]*ports:/ {print NR": in service "svc}' \
  infra/docker-compose.prod.yml
```

Expected: **exactly one line**, and it must name `caddy`. For example:

```
9: in service caddy
```

The line number depends on your formatting and does not matter. The service name does. Two lines
means a second service publishes a port.

```bash
# 2. No host networking — it publishes ports without a ports: key
grep -nE 'network_mode|pid:[[:space:]]*host' infra/docker-compose.prod.yml
```

Expected: no output at all.

```bash
# 3. The .env is not about to be committed
git check-ignore -v infra/.env
```

Expected: a line confirming the ignore rule matched.

## A9. Commit

```bash
git add infra/ .gitignore
git commit -m "infra: production compose stack, Caddy, Prometheus rules"
```

---

# PART B — DNS `[LOCAL]`

Do this before Caddy ever starts. Certificate issuance fails without it, and repeated failures count
against Let's Encrypt rate limits.

Create two A records at your registrar:

```
nimbus.<domain>     A    <vps-ip>
grafana.<domain>    A    <vps-ip>
```

Verify before continuing:

```bash
dig +short nimbus.<domain>
dig +short grafana.<domain>
```

Both must print your VPS IP. If either is empty, wait and re-check.

---

# PART C — Host baseline `[VPS]`

## C0. Audit — what is already done

Run this first. It tells you which of C1–C8 you can skip.

```bash
ssh deploy@<vps-ip> '
echo -n "os:            "; lsb_release -ds
echo -n "deploy sudo:   "; sudo -n true 2>/dev/null && echo ok || echo "BROKEN — see C2"
echo -n "root ssh off:  "; sudo sshd -T | grep -q "permitrootlogin no" && echo ok || echo "TODO C3"
echo -n "passwd off:    "; sudo sshd -T | grep -q "passwordauthentication no" && echo ok || echo "TODO C3"
echo -n "ufw:           "; sudo ufw status | head -1
echo -n "fail2ban:      "; systemctl is-active fail2ban 2>/dev/null || echo "TODO C4"
echo -n "docker:        "; docker --version 2>/dev/null || echo "TODO C5"
echo -n "docker group:  "; id -nG | grep -qw docker && echo ok || echo "TODO C5 (or re-login)"
echo -n "swap:          "; free -m | awk "/Swap:/ {print \$2\" MB\"}"
echo -n "opt/nimbus:    "; stat -c "%a %U:%G" /opt/nimbus 2>/dev/null || echo "TODO C8"
echo    "data dirs:"; ls -ln /srv/nimbus/data 2>/dev/null || echo "  TODO C7"
'
```

## C1. `deploy` user `[VPS as root]`

Skip if C0 showed `deploy sudo: ok`.

Run as `root`, either over SSH with the initial Contabo password or via the panel's VNC console.

**Get your public key first** `[LOCAL]`:

```bash
cat ~/.ssh/id_ed25519.pub
```

That is one line with three space-separated fields; the middle base64 blob is **68 characters**. A
43-character string is a fingerprint, not a key — it will not work.

Then, as root on the VPS, in one block:

```bash
adduser --disabled-password --gecos "" deploy
usermod -aG sudo deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh

cat > /home/deploy/.ssh/authorized_keys <<'EOF'
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI...PASTE-YOUR-WHOLE-KEY-LINE-HERE... nickt@Mac
EOF

chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys
chmod 750 /home/deploy

echo 'deploy ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/90-deploy
chmod 440 /etc/sudoers.d/90-deploy
visudo -c

ssh-keygen -lf /home/deploy/.ssh/authorized_keys
```

The last command must print a fingerprint matching `ssh-keygen -lf ~/.ssh/id_ed25519.pub` on your
Mac. If it says `is not a public key file`, the file is empty or the key was pasted wrong — fix it
now, before C3.

The `NOPASSWD` sudoers rule is required, not optional: `--disabled-password` means `deploy` has no
password, so a `sudo` prompt would be unanswerable.

## C2. GATE — verify before hardening anything

**Do not proceed past this point until this passes.** Everything after C2 can lock you out;
everything before it cannot.

From your Mac, in a **new** terminal:

```bash
ssh deploy@<vps-ip> 'id && sudo -n true && echo GATE_PASSED'
```

You need `deploy` in the `sudo` group and `GATE_PASSED`.

Why a fresh terminal matters: `systemctl restart ssh` does not drop existing connections, so an
already-open session keeps working even with a broken config. The breakage only appears at the next
reboot. Test on a new connection, and keep the old one open until the new one works.

## C3. SSH hardening `[VPS]`

**Open a second SSH session now and leave it connected** while you do this.

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

The file must be named `10-`, not `99-`. `sshd_config` includes `sshd_config.d/*.conf` at the top and
uses **first-value-wins**, so a `99-` file loses to Contabo's `50-cloud-init.conf` and password auth
silently stays enabled.

Required output from that last command:

```
allowusers deploy
permitrootlogin no
passwordauthentication no
kbdinteractiveauthentication no
```

`sshd -T` shows the merged effective config. The contents of your own file prove nothing.

Now, from a **third** terminal:

```bash
ssh deploy@<vps-ip> 'echo OK'    # must succeed
ssh root@<vps-ip>                # must be refused
```

Only then close the other sessions.

## C4. Firewall and fail2ban `[VPS]`

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable

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

`backend = systemd` is required on Ubuntu 24.04 — sshd logs to the journal, not to a file fail2ban
can tail. Without it the jail loads, matches nothing, and reports zero bans forever.

Port 80 must be open even though the app is HTTPS-only: Let's Encrypt's HTTP-01 challenge uses it.

## C5. Docker `[VPS]`

```bash
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo tee /etc/docker/daemon.json > /dev/null <<'EOF'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "50m",
    "max-file": "3"
  },
  "live-restore": true
}
EOF
```

This must match `infra/daemon.json` in the repo (issue #103) — log rotation is set daemon-wide so it
can't be forgotten on a service added later.

```bash
sudo systemctl restart docker
sudo systemctl enable docker
sudo usermod -aG docker deploy
```

**Now log out and back in**, or `docker ps` will still need `sudo`:

```bash
exit
ssh deploy@<vps-ip>
docker ps          # must work without sudo
```

`sudo tee`, not `sudo echo >` — the redirect runs in your unprivileged shell before sudo, so
`sudo echo x > /etc/file` fails with permission denied.

The log caps matter: containers write stdout to disk via json-file *in addition* to shipping to Loki,
uncapped by default.

## C6. Swap `[VPS]`

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

Low swappiness keeps swap as a cushion for a PDF-render spike rather than a working tier. The real
protection is the `mem_limit` on SQL Server in the compose file — otherwise the OOM killer picks the
largest process, which is your database, not the transient render that caused the pressure.

## C7. Data directories `[VPS]`

These UIDs belong to users **inside the container images**, not accounts on the host. Docker would
create missing bind-mount sources as `root:root`, and then SQL Server (10001) and Grafana (472) cannot
write to their own data. So create them explicitly:

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

`mssql` gets mode `770` rather than `750`: the directory is group `0` and SQL Server writes as
`10001:0`, so it needs group write as well as owner write.

Use `ls -ln`, not `ls -l` — numeric UIDs, because the names do not exist on the host.

> **Do this before any `docker compose up -d`.** If a bind-mount source is missing when a container
> starts, Docker creates it as `root:root` — and `install -d` later will not undo that for
> subdirectories Docker already made. SQL Server, Loki and Grafana then fail to write to their own
> data and sit in a restart loop. See D5b if you have already hit this.

### Directory-size metric

Root is one filesystem, so node_exporter reports a single figure for `/` and cannot tell you whether
MinIO or Loki is the one filling it. This adds a per-directory metric:

```bash
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
    echo "nimbus_directory_size_bytes{directory=\"${name}\"} ${size}"
  done
} > "$TMP"
mv "$TMP" "$OUT"
EOF

sudo chmod 755 /usr/local/bin/nimbus-dirsize.sh

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

sudo systemctl daemon-reload
sudo systemctl enable --now nimbus-dirsize.timer
sudo /usr/local/bin/nimbus-dirsize.sh
cat /var/lib/node_exporter/textfile_collector/nimbus_dirsize.prom
```

The quoted `<<'EOF'` is required. Unquoted, your shell would expand `$$`, `$d` and `${OUT}` while
writing the file and you would install a script with values baked in instead of variables.

The write-to-temp-then-`mv` matters too: node_exporter reading a half-written `.prom` file produces
parse errors and gaps.

## C8. Unattended upgrades and `/opt/nimbus` `[VPS]`

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

Must read `750 deploy:deploy`. Use `install -d` with `-o`/`-g`, not `sudo mkdir` — the latter gives
`root:root` and Part D's `scp` then fails with a permission error that looks like an SSH problem.

The quoted heredoc is critical here: unquoted, `${distro_id}` expands to an empty string and
`Allowed-Origins` becomes `":-security"`, which matches nothing. No security updates would ever
install, and nothing would report an error.

`Automatic-Reboot "false"` means kernel updates download but wait for you. Check
`/var/run/reboot-required` periodically.

---

# PART D — Deploy and verify

## D1. Copy the files `[LOCAL]`

Note the destination filename: `compose.yaml`. Compose picks that up by default, so you never type
`-f` on the server.

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/docker-compose.limits.yml deploy@<vps-ip>:/opt/nimbus/compose.override.yaml
scp infra/Caddyfile infra/prometheus.yml infra/alert.rules.yml infra/minio-init.sh deploy@<vps-ip>:/opt/nimbus/
ssh deploy@<vps-ip> 'ls -l /opt/nimbus'
```

`compose.override.yaml` is `infra/docker-compose.limits.yml` (issue #103) renamed on the server —
Compose auto-loads `compose.yaml` + `compose.override.yaml` together, so every command below still
needs no `-f` flag and carries the memory/CPU ceilings automatically.

## D2. Create `.env` `[VPS]`

Generate the secrets first and keep the output — you will paste these into your password manager:

```bash
echo "SA:           $(openssl rand -base64 24)"
echo "MINIO:        $(openssl rand -base64 24)"
echo "MINIO_APP:    $(openssl rand -base64 24)"
echo "GRAFANA:      $(openssl rand -base64 24)"
echo "MINIO_CONSOLE_PW: $(openssl rand -base64 18)"
docker run --rm caddy:2-alpine caddy hash-password --plaintext '<paste MINIO_CONSOLE_PW above>'
```

Then write the file, substituting your real values:

```bash
cat > /opt/nimbus/.env <<'EOF'
NIMBUS_DOMAIN=example.be
ACME_EMAIL=you@example.be
ASPNETCORE_ENVIRONMENT=Production
IMAGE_TAG=latest

MSSQL_SA_PASSWORD=<paste SA>
MSSQL_PID=Express
MSSQL_MEMORY_LIMIT_MB=1792

MINIO_ROOT_USER=nimbus
MINIO_ROOT_PASSWORD=<paste MINIO>
MINIO_APP_ACCESS_KEY=nimbus-app
MINIO_APP_SECRET_KEY=<paste MINIO_APP>
MINIO_CONSOLE_USER=nick
MINIO_CONSOLE_PASSWORD_HASH=<paste output of `caddy hash-password`>

GRAFANA_ADMIN_USER=nick
GRAFANA_ADMIN_PASSWORD=<paste GRAFANA>

Loki__Url=http://loki:3100

RESTIC_REPOSITORY=
RESTIC_PASSWORD=

EMAIL_PROVIDER_API_KEY=
EMAIL_FROM=
EOF

chmod 600 /opt/nimbus/.env
stat -c '%a %U:%G' /opt/nimbus/.env
```

`NIMBUS_DOMAIN` is the bare domain — Caddy prepends `nimbus.` and `grafana.` itself.

SQL Server rejects weak passwords at startup with a message that does not obviously say so. If
`openssl rand -base64` gives you a string without a digit, regenerate it.

## D3. Check interpolation before starting anything `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub config > /dev/null && echo "CONFIG OK"
docker compose --profile stub config | \
  awk '/^  [a-z0-9_-]+:/ {svc=$1} /^[[:space:]]*ports:/ {print NR": in service "svc}'
```

First command must print `CONFIG OK`. Second must print exactly one line naming `caddy`. This is the
authoritative view — it resolves every variable and override.

If a `${VAR}` resolved to an empty string, a key is missing from `.env`. Much easier to see here than
as a container that starts and instantly exits.

## D4. Start `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub up -d
docker compose --profile stub up minio-init   # one-shot: bootstraps buckets/policy/app-user, exits 0
docker compose --profile stub ps
```

Watch certificate issuance:

```bash
docker compose logs -f caddy
```

You want `certificate obtained successfully` for all three hostnames (`nimbus.`, `grafana.`,
`console.`). `Ctrl+C` to stop following.

ACME failures are almost always Part B (DNS not resolving) or port 80 blocked.

## D5. GATE — confirm the stack is genuinely up `[VPS]`

**A port scan against a dead stack is a false pass.** If containers failed to start, nothing publishes
80/443 and `nmap` returns a beautifully clean result — because nothing is running, not because your
config is right.

```bash
cd /opt/nimbus
docker compose --profile stub ps --format 'table {{.Service}}\t{{.State}}'
```

All eight long-running services must read `running`: `api-stub`, `caddy`, `grafana`, `loki`, `minio`,
`node-exporter`, `prometheus`, `sqlserver`. `minio-init` is one-shot and correctly shows `exited (0)`
— re-run `docker compose --profile stub up minio-init` any time to confirm it is still idempotent.

```bash
curl -sI http://localhost | head -1        # Caddy is answering
```

Anything not running:

```bash
docker compose logs --tail=50 <service>
```

Do not continue to D6 until all eight are up.

### D5b. If a container is stuck restarting

`restarting` means it starts, fails, and Docker retries. Get all the failing logs at once:

```bash
cd /opt/nimbus
docker compose logs --tail=30 sqlserver loki grafana
ls -ln /srv/nimbus/data
```

| Log line | Cause | Fix |
|---|---|---|
| `The system directory [/.system] could not be created ... Permission denied` | `mssql` dir not writable by `10001`. The path in the message is misleading — SQL Server is failing to establish its data directory, not writing to container root | `sudo chown -R 10001:0 /srv/nimbus/data/mssql && sudo chmod 770 /srv/nimbus/data/mssql` |
| `Unable to open the physical file ... error 5` | same, on an existing database file | same |
| `Password validation failed` | SA password fails SQL Server's policy: 8+ chars from 3 of 4 classes (upper, lower, digit, symbol) | regenerate, update `.env`, `docker compose up -d sqlserver` |
| `GF_PATHS_DATA='/var/lib/grafana' is not writable` | `grafana` dir not owned by `472` | `sudo chown -R 472:0 /srv/nimbus/data/grafana` |
| `mkdir /loki/rules: permission denied` | `loki` dir not owned by `10001` | `sudo chown -R 10001:10001 /srv/nimbus/data/loki` |
| `error loading config ... alert.rules.yml` | rules file missing or malformed | re-copy from A5; Prometheus refuses to start rather than skipping it |
| `no such file or directory` on a mounted config | file not copied in D1 | re-run D1, check `ls -l /opt/nimbus` |

If `ls -ln` shows `0 0` on a directory that should have a container UID, Docker created it itself —
which happens if `up -d` ran before C7. Docker creates missing bind-mount sources as `root:root`, and
the container then cannot write to its own data. Correct all three at once:

```bash
sudo chown -R 10001:0     /srv/nimbus/data/mssql
sudo chown -R 10001:10001 /srv/nimbus/data/loki
sudo chown -R 472:0       /srv/nimbus/data/grafana
sudo chown -R 1000:1000   /srv/nimbus/data/minio
sudo chown -R 65534:65534 /srv/nimbus/data/prometheus
sudo chmod 770 /srv/nimbus/data/mssql
docker compose --profile stub up -d
```

`-R` is not optional. A container in a restart loop often creates partial subdirectories as root
before failing, and a non-recursive `chown` leaves those behind — the container then fails on the
same path with the parent looking correct.

A restart loop is not harmless while you diagnose it: each cycle writes to the Docker log and
`restart: unless-stopped` never gives up. If you are stepping away mid-debug, `docker compose stop
<service>` rather than leaving it spinning.

## D6. Verify what Docker published `[VPS]`

```bash
docker ps --format 'table {{.Names}}\t{{.Ports}}'
sudo ss -tlnp | grep -vE '127\.|\[::1\]'
```

Only the Caddy row in `docker ps` should show `->` arrows; every other row lists internal ports with
no mapping.

The `ss` output should show exactly this and nothing more:

```
LISTEN  0  4096   0.0.0.0:22    users:(("sshd",...))
LISTEN  0  4096   0.0.0.0:80    users:(("docker-proxy",...))
LISTEN  0  4096   0.0.0.0:443   users:(("docker-proxy",...))
LISTEN  0  4096      [::]:22    users:(("sshd",...))
LISTEN  0  4096      [::]:80    users:(("docker-proxy",...))
LISTEN  0  4096      [::]:443   users:(("docker-proxy",...))
```

The filter is `127\.` rather than `127\.0\.0\.1` on purpose: systemd-resolved listens on
`127.0.0.53` and `127.0.0.54`, which are loopback and therefore unreachable from outside, but a
narrower pattern leaves them in the output and makes a clean result look dirty.

Anything bound to `0.0.0.0` or `[::]` other than 22, 80 and 443 is publicly reachable and needs
explaining before you continue.

## D7. External scan — the acceptance criterion `[LOCAL]`

`ufw status` and `docker ps` do not see what the internet sees. This must run from a machine that is
not the VPS, with the stack up.

```bash
nmap -Pn -p1-65535 <vps-ip>
```

Required result, nothing else:

```
PORT    STATE SERVICE
22/tcp  open  ssh
80/tcp  open  http
443/tcp open  https
```

If 1433, 3000, 3100, 9000, 9001, 9090 or 9100 appear, go back to D3.

Then confirm the proxy routes correctly:

```bash
curl -sI https://nimbus.<domain>  | head -1     # 200 — the whoami stub
curl -sI https://grafana.<domain> | head -1     # 200 or 302
curl -skI https://<vps-ip>        | head -1     # must NOT be Grafana
curl -sI  http://<vps-ip>:3000                  # must fail: connection refused
```

Open `https://grafana.<domain>` in a browser and log in with your `.env` credentials.

**Paste the `nmap` output plus the `docker compose ps` output into issue #5 together.** The scan only
means something alongside proof that the stack was alive.

---

# PART E — Later: switching to the real API

When CI pushes images, this is the entire change. **No files are edited.**

```bash
# [VPS] one-time: GHCR packages are private by default
echo '<PAT with read:packages>' | docker login ghcr.io -u NickThys3012 --password-stdin

cd /opt/nimbus
docker compose --profile stub down
docker compose --profile app up -d
docker compose --profile app ps
```

Then re-run D5, D6 and D7. Adding services is exactly when a port policy decays, so the second scan
is not a formality — and it is the one that satisfies the acceptance criterion.

If the pull fails with `denied` or `manifest unknown`, that is the GHCR visibility issue above, not a
missing image.

---

# PART F — Verification checklist for the issue

| # | Criterion | Command |
|---|---|---|
| 1 | Ubuntu 24.04 | `lsb_release -ds` |
| 2 | `deploy` in `sudo` + `docker` | `id deploy` |
| 3 | Key present and correct | `ssh-keygen -lf /home/deploy/.ssh/authorized_keys` |
| 4 | Root and password login off | `sudo sshd -T \| grep -Ei 'permitrootlogin\|passwordauth\|allowusers'` |
| 5 | ufw allows only 22/80/443 | `sudo ufw status verbose` |
| 6 | fail2ban matching | `sudo fail2ban-client status sshd` |
| 7 | Docker from official repo | `apt-cache policy docker-ce \| grep download.docker.com` |
| 8 | Swap active, swappiness 10 | `swapon --show && sysctl vm.swappiness` |
| 9 | Data dirs with right UIDs | `ls -ln /srv/nimbus/data` |
| 10 | Directory-size metric live | `cat /var/lib/node_exporter/textfile_collector/nimbus_dirsize.prom` |
| 11 | Unattended upgrades on | `sudo unattended-upgrades --dry-run --debug \| tail -20` |
| 12 | `.env` is 600 deploy:deploy | `stat -c '%a %U:%G' /opt/nimbus/.env` |
| 13 | **Stack was up when scanned** | `docker compose ps` — all eight `running` |
| 14 | **External scan clean** | `nmap -Pn -p1-65535 <ip>` from off-host |
| 15 | Grafana over TLS, not on 3000 | `curl -sI https://grafana.<domain>` |
| 16 | Rebuild time recorded | table below |

| Phase | Elapsed |
|---|---|
| Panel reinstall → SSH answers | ___ min |
| Part C (baseline) | ___ min |
| Part D (`up -d` → healthy) | ___ min |
| **Total** | **___ min** |

---

# PART G — Break-glass recovery

**Requires the Contabo root password. Put it in your password manager now, not later.**

`Permission denied (publickey)` means sshd is running and reachable and rejected your key — a
firewall problem would give a timeout instead. So the fix is server-side, and nothing you type on
your Mac will help.

**Contabo panel → your VPS → VNC / Console.** That attaches to the machine's console device and does
not go through sshd, so none of your hardening applies. Log in as `root`.

Diagnostic block:

```bash
grep -r . /etc/ssh/sshd_config.d/
sshd -T | grep -Ei 'allowusers|permitrootlogin|passwordauthentication'
id deploy
ls -ld /home/deploy /home/deploy/.ssh; ls -l /home/deploy/.ssh/
ssh-keygen -lf /home/deploy/.ssh/authorized_keys
ls -l /etc/sudoers.d/; visudo -c
tail -40 /var/log/auth.log
```

| Symptom | Cause | Fix |
|---|---|---|
| `authorized_keys` size 0 | key never written | redo C1's heredoc |
| `is not a public key file` | fingerprint pasted instead of key | 68-char blob, not 43 |
| fingerprint mismatch | wrong key | install the right one |
| `AllowUsers {{ ... }}` | template pasted literally | `AllowUsers deploy` |
| `/home/deploy` world-writable | sshd silently refuses | `chmod 750 /home/deploy` |
| `not listed in AllowUsers` | account not permitted | fix `AllowUsers` |
| `sudo` prompts for password | missing sudoers drop-in | redo C1's sudoers block |
| `scp: Permission denied` | `/opt/nimbus` owned by root | `sudo chown -R deploy:deploy /opt/nimbus` |

Fix, then verify **before leaving the console**:

```bash
sshd -t && systemctl restart ssh
sshd -T | grep -Ei 'allowusers|permitrootlogin|passwordauthentication'
ssh-keygen -lf /home/deploy/.ssh/authorized_keys
su - deploy -c 'sudo -n true && echo SUDO_OK'
```

Keep the console open until a real SSH login from your Mac succeeds.

**On an empty host, a panel reinstall is ~10 minutes.** If a lockout has cost more than that,
reinstall and paste your public key into the panel's SSH key field — that prevents this whole class of
failure, and it gives you the timed rebuild Part F needs anyway.

---

# Still outstanding for issue #5

Part C is currently a set of shell commands. The acceptance criteria ask for it as an **idempotent
Ansible role under `infra/`**, so that the rebuild is reproducible rather than remembered. That is a
separate deliverable — the commands above are what it should encode, one task per block.

Also deferred, correctly, to Sprint 0b: restic backups and a rehearsed restore, Alertmanager or
Grafana notification routing, Loki/Prometheus retention policy, and Grafana dashboard provisioning.
