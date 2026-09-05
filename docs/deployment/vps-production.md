# Elyndor production VPS

This deployment path is intentionally lightweight for a small Linux VPS. The
production host runs only PostgreSQL, the ASP.NET Core runtime, Elyndor, and
Caddy. Node.js, the .NET SDK, Aspire, Docker, and Vite stay off the VPS.

The first target is Ubuntu 26.04 LTS on x86_64 with 1 vCPU, 1 GB RAM, and a
10 GB disk.

## Runtime layout

```text
Telegram / browser
        |
      HTTPS
        |
      Caddy
        |
127.0.0.1:5080
        |
 Elyndor.Server
        |
127.0.0.1:5432
        |
 PostgreSQL 18
```

Only SSH, HTTP, and HTTPS should be reachable from the public internet.
PostgreSQL and port 5080 remain loopback-only.

## 1. Baseline the server

Run as root:

```bash
apt update
apt upgrade -y
apt install -y ca-certificates curl gnupg ufw postgresql-common
```

Check memory and existing swap before creating more:

```bash
free -h
swapon --show
df -h /
```

For a 1 GB host, keep roughly 1 GB of swap available. Do not create a second
swap file when the provider already configured sufficient swap.

Open only the required public ports:

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```

## 2. Install .NET 10

Ubuntu 26.04 provides .NET 10 in the built-in Ubuntu feed:

```bash
apt install -y aspnetcore-runtime-10.0
dotnet --list-runtimes
```

Do not install the SDK on the production VPS. Releases are built elsewhere.

## 3. Install PostgreSQL 18

Configure the official PostgreSQL Apt Repository:

```bash
/usr/share/postgresql-common/pgdg/apt.postgresql.org.sh
apt update
apt install -y postgresql-18
systemctl enable --now postgresql
```

Create a dedicated login and database without putting the database password in
shell history:

```bash
sudo -u postgres psql
```

Then in `psql`:

```text
CREATE ROLE elyndor LOGIN;
\password elyndor
CREATE DATABASE game OWNER elyndor;
\q
```

Keep PostgreSQL bound to localhost. Port 5432 must not be exposed publicly.

## 4. Create the application account and directories

```bash
useradd --system --create-home --home-dir /var/lib/elyndor --shell /usr/sbin/nologin elyndor
mkdir -p /opt/elyndor/releases /etc/elyndor /var/backups/elyndor
chmod 700 /etc/elyndor
```

Install the unit file from this repository:

```bash
install -m 0644 deploy/elyndor.service /etc/systemd/system/elyndor.service
systemctl daemon-reload
systemctl enable elyndor
```

Copy `deploy/elyndor.env.example` to `/etc/elyndor/elyndor.env`, replace all
`CHANGE_ME` values, and protect it:

```bash
chmod 600 /etc/elyndor/elyndor.env
chown root:root /etc/elyndor/elyndor.env
```

Production must keep:

```text
Content__AllowFileFallbackOnRestoreFailure=false
Authentication__Development__Enabled=false
```

The development identity must never be enabled on this host.

## 5. Build a release away from the VPS

A manual GitHub Actions workflow named `package-production` produces the
`elyndor-linux-x64` artifact.

The same archive can be built on a Linux development machine:

```bash
./deploy/build-release.sh
```

The release archive contains the published .NET application, game content, and
the built Vue frontend. It never contains `.env` files or Telegram secrets.

## 6. Install or update a release

Copy the archive and `deploy/install-release.sh` to the VPS, then run:

```bash
chmod 750 /usr/local/sbin/elyndor-deploy
/usr/local/sbin/elyndor-deploy /tmp/elyndor-linux-x64.tar.gz
```

The deploy script:

- extracts to a versioned directory under `/opt/elyndor/releases`;
- changes the `current` symlink atomically;
- restarts the systemd service;
- checks `http://127.0.0.1:5080/alive`;
- automatically restores the previous release when the health check fails;
- retains the three newest successful release directories.

Useful commands:

```bash
systemctl status elyndor
journalctl -u elyndor -n 200 --no-pager
curl -fsS http://127.0.0.1:5080/alive
```

## 7. HTTPS with Caddy

Point an A record such as `game.example.com` at the VPS public IPv4 address.
Install Caddy using its official package repository, then copy
`deploy/Caddyfile.example` to `/etc/caddy/Caddyfile` and replace the example
hostname.

Caddy proxies only to the loopback Elyndor endpoint and handles public TLS.

After changing the configuration:

```bash
caddy validate --config /etc/caddy/Caddyfile
systemctl reload caddy
```

Verify:

```bash
curl -I https://game.example.com/
curl -fsS https://game.example.com/alive
```

## 8. Telegram

After HTTPS works, change the Telegram Mini App / menu button URL to the
production hostname.

Register the administration webhook at:

```text
https://game.example.com/api/v1/administration/telegram/webhook
```

Use the same bot token and webhook secret stored in
`/etc/elyndor/elyndor.env`. Never commit either value.

## 9. Database backups

Run `deploy/backup-db.sh` as root. By default it creates PostgreSQL custom
format dumps in `/var/backups/elyndor` and removes dumps older than seven
days.

A VPS-local backup is useful for fast rollback, but it is not sufficient as the
only backup. Copy important backups to another machine or storage location.

## Local database migration

Before switching the Telegram URL, dump the current development database,
transfer the dump to the VPS, restore it into the `game` database, start
Elyndor, and verify the character, content release, inventory, and admin state.

Do not delete the local Aspire PostgreSQL volume until the VPS copy has been
verified and backed up.
