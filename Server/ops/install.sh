#!/bin/bash
# Run once as root, with the reviewed GitHub commit SHA as the argument.
set -euo pipefail
[[ $EUID == 0 ]] || { echo 'Run with sudo bash'; exit 1; }
revision=${1:?Pass the installation commit SHA}
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || exit 1
[[ $(node -p 'process.versions.node.split(".")[0]') -ge 24 ]]
if ! command -v git >/dev/null; then
  dnf --disablerepo='*' --enablerepo=ol9_baseos_latest,ol9_appstream install -y git
fi
for account in fishing-server fishing-deploy; do
  id "$account" >/dev/null 2>&1 || useradd --system --no-create-home --shell /sbin/nologin "$account"
done
install -d -m 755 /opt/fishing-server /usr/local/lib/fishing-server
install -d -m 755 -o fishing-deploy -g fishing-deploy /opt/fishing-server/source /opt/fishing-server/releases /opt/fishing-server/npm-cache
# Writable coordinator state gets its own persistent SELinux label.
install -d -m 755 /var/lib/fishing-deployment
if command -v selinuxenabled >/dev/null && selinuxenabled; then
  if ! command -v semanage >/dev/null; then
    dnf --disablerepo='*' --enablerepo=ol9_baseos_latest,ol9_appstream install -y policycoreutils-python-utils
  fi
  semanage fcontext -a -t init_var_lib_t '/var/lib/fishing-deployment(/.*)?' ||
    semanage fcontext -m -t init_var_lib_t '/var/lib/fishing-deployment(/.*)?'
  restorecon -RF /var/lib/fishing-deployment
fi
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
for name in prepare.sh update.sh; do
  curl --fail --show-error --silent --location --retry 3 --connect-timeout 15 --max-time 120 \
    "https://raw.githubusercontent.com/GalacticSlayer/3D-Android-Open-World/$revision/Server/ops/$name" -o "$work/$name"
  bash -n "$work/$name"
  install -m 755 "$work/$name" "/usr/local/lib/fishing-server/$name"
done
cat > /etc/systemd/system/fishing-server.service <<'UNIT'
[Unit]
Description=Fishing game backend
After=network.target
StartLimitIntervalSec=0

[Service]
Type=simple
User=fishing-server
Group=fishing-server
WorkingDirectory=/opt/fishing-server/current
Environment=NODE_ENV=production HOST=127.0.0.1 PORT=3000
EnvironmentFile=/var/lib/fishing-deployment/release.env
ExecStart=/usr/bin/node --max-old-space-size=160 server.mjs
Restart=always
RestartSec=5
TimeoutStopSec=10
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
StateDirectory=fishing-server
Environment=DATA_DIR=/var/lib/fishing-server
MemoryMax=256M
MemorySwapMax=256M
TasksMax=64
StandardOutput=journal
StandardError=journal
LogRateLimitIntervalSec=30s
LogRateLimitBurst=200

[Install]
WantedBy=multi-user.target
UNIT
cat > /etc/systemd/system/fishing-update.service <<'UNIT'
[Unit]
Description=Fetch and health-check fishing backend updates
Wants=network-online.target
After=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/lib/fishing-server/update.sh
TimeoutStartSec=10min
MemoryMax=512M
MemorySwapMax=1G
CPUQuota=50%
Nice=10
UNIT
cat > /etc/systemd/system/fishing-update.timer <<'UNIT'
[Unit]
Description=Check GitHub for fishing backend updates every five minutes

[Timer]
OnBootSec=2min
OnUnitInactiveSec=5min
RandomizedDelaySec=15s
Unit=fishing-update.service

[Install]
WantedBy=timers.target
UNIT
systemctl daemon-reload
systemctl enable fishing-server.service
systemctl start fishing-update.service
systemctl enable --now fishing-update.timer
curl -fsS http://127.0.0.1:3000/health
echo
systemctl --no-pager list-timers fishing-update.timer
echo 'Installation complete. Runtime updates are now automatic.'
