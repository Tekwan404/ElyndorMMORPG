# Elyndor — полный production runbook для REG.RU VPS

Этот документ — единая инструкция по развертыванию, эксплуатации и обновлению
production-инстанса Elyndor на небольшом Linux VPS.

Документ описывает текущую production-схему проекта и должен обновляться вместе
с deployment-кодом.

## 1. Текущий production target

На момент первоначального развертывания используется:

```text
Провайдер:        REG.RU Cloud
Тариф:            Free Tier C1-M1-D10
ОС:               Ubuntu 26.04 LTS
Архитектура:      x86_64
vCPU:             1
RAM:              955 MiB (~1 GB)
Swap:             1 GiB
Disk:             8.6 GiB usable
Public IPv4:      194.226.97.122
Private IPv4:     192.168.0.135
Game host:        game.elyndor.su
Admin host:       admin.elyndor.su
```

Порты 25 и 465, заблокированные провайдером, Elyndor не нужны.

## 2. Production architecture

```text
Telegram Mini App / Browser
            |
          HTTPS
            |
     game.elyndor.su
            |
          Caddy
          :443
            |
      127.0.0.1:5080
            |
      Elyndor.Server
     ASP.NET Core 10
            |
      127.0.0.1:5432
            |
      PostgreSQL 18
```

На VPS намеренно НЕ используются:

```text
Docker
Docker Compose
Aspire
Vite dev server
Node.js runtime
.NET SDK
Tailscale Funnel
```

Production-хост только запускает заранее собранный release.

Frontend Vue собирается вне VPS и поставляется вместе с опубликованным
ASP.NET Core приложением.

## 3. Что разрешено из интернета

Публично доступны только:

```text
22/tcp   SSH
80/tcp   HTTP (ACME / redirect to HTTPS)
443/tcp  HTTPS
```

Не должны быть опубликованы:

```text
5080/tcp Elyndor origin
5432/tcp PostgreSQL
```

Проверка:

```bash
ufw status verbose
ss -lntp | grep -E ':(22|80|443|5080|5432)\b'
```

Ожидается:

```text
127.0.0.1:5080
127.0.0.1:5432
```

а не `0.0.0.0:5080` / `0.0.0.0:5432`.

---

# Первоначальная установка VPS

## 4. Подключение по SSH

С Windows:

```powershell
ssh root@194.226.97.122
```

Проверить сервер:

```bash
whoami
cat /etc/os-release
uname -m
free -h
swapon --show
df -h /
```

Для текущего сервера ожидается `root`, Ubuntu 26.04 LTS и `x86_64`.

## 5. Обновление системы

```bash
apt update
apt upgrade -y
apt install -y ca-certificates curl gnupg ufw
```

## 6. Swap для VPS с 1 GB RAM

Сначала обязательно проверить существующий swap:

```bash
swapon --show
free -h
```

Если swap отсутствует, создать 1 GiB:

```bash
fallocate -l 1G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

Добавить настройки:

```bash
cat >/etc/sysctl.d/99-elyndor.conf <<'EOF'
vm.swappiness=10
vm.vfs_cache_pressure=50
EOF

sysctl --system
```

Проверка:

```bash
free -h
swapon --show
```

Для текущего production-хоста ожидается примерно:

```text
Swap: 1.0Gi
/swapfile 1024M
```

Не создавать второй swap-файл, если провайдер уже настроил достаточный swap.

## 7. Firewall

До включения UFW убедиться, что SSH-подключение работает.

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
ufw status verbose
```

После включения firewall рекомендуется открыть второй терминал Windows и
проверить новое SSH-соединение:

```powershell
ssh root@194.226.97.122
```

---

# Runtime

## 8. ASP.NET Core Runtime 10

На Ubuntu 26.04 устанавливается только runtime:

```bash
apt update
apt install -y aspnetcore-runtime-10.0
```

Проверка:

```bash
dotnet --list-runtimes
```

Должны присутствовать:

```text
Microsoft.AspNetCore.App 10.0.x
Microsoft.NETCore.App 10.0.x
```

.NET SDK на production VPS не нужен.

## 9. PostgreSQL 18

Установить `postgresql-common`:

```bash
apt install -y postgresql-common
```

Подключить официальный PGDG repository:

```bash
/usr/share/postgresql-common/pgdg/apt.postgresql.org.sh
apt update
apt install -y postgresql-18
systemctl enable --now postgresql
```

Проверка:

```bash
psql --version
pg_lsclusters
systemctl status postgresql --no-pager
```

Ожидается cluster PostgreSQL 18 на порту 5432.

## 10. Создание роли и базы Elyndor

Открыть psql:

```bash
sudo -u postgres psql
```

Создать роль:

```sql
CREATE ROLE elyndor LOGIN;
\password elyndor
```

Пароль вводится интерактивно. Не сохранять production DB password в Git и не
отправлять его в чат.

Создать базу:

```sql
CREATE DATABASE game OWNER elyndor;
```

Для VPS с 1 GB RAM применить консервативные настройки:

```sql
ALTER SYSTEM SET max_connections = '30';
ALTER SYSTEM SET shared_buffers = '128MB';
ALTER SYSTEM SET work_mem = '4MB';
ALTER SYSTEM SET maintenance_work_mem = '32MB';
ALTER SYSTEM SET effective_cache_size = '512MB';
ALTER SYSTEM SET listen_addresses = 'localhost';
\q
```

Перезапустить PostgreSQL:

```bash
systemctl restart postgresql
```

Проверить bind:

```bash
ss -lntp | grep 5432
```

Ожидается:

```text
127.0.0.1:5432
```

Проверить роль и базу:

```bash
sudo -u postgres psql -tAc "SELECT rolname FROM pg_roles WHERE rolname='elyndor';"
sudo -u postgres psql -tAc "SELECT datname FROM pg_database WHERE datname='game';"
```

Ожидается:

```text
elyndor
game
```

Проверка входа:

```bash
psql -h 127.0.0.1 -U elyndor -d game
```

Внутри:

```sql
SELECT current_database(), current_user, version();
\q
```

---

# Установка Elyndor

## 11. Системный пользователь и каталоги

```bash
useradd --system \
  --create-home \
  --home-dir /var/lib/elyndor \
  --shell /usr/sbin/nologin \
  elyndor

mkdir -p \
  /opt/elyndor/releases \
  /etc/elyndor \
  /var/backups/elyndor

chmod 700 /etc/elyndor
```

Если пользователь уже существует, повторно создавать его не нужно.

Текущая layout:

```text
/opt/elyndor/
  current -> releases/<release-id>
  releases/

/etc/elyndor/
  elyndor.env

/var/lib/elyndor/

/var/backups/elyndor/
```

## 12. Установка deployment helper'ов

Загрузить их из `main`:

```bash
curl -fsSL \
  https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/main/deploy/elyndor.service \
  -o /etc/systemd/system/elyndor.service

curl -fsSL \
  https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/main/deploy/install-release.sh \
  -o /usr/local/sbin/elyndor-deploy

curl -fsSL \
  https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/main/deploy/backup-db.sh \
  -o /usr/local/sbin/elyndor-backup

curl -fsSL \
  https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/main/deploy/elyndor.env.example \
  -o /etc/elyndor/elyndor.env
```

Права:

```bash
chmod 750 /usr/local/sbin/elyndor-deploy
chmod 750 /usr/local/sbin/elyndor-backup

chmod 600 /etc/elyndor/elyndor.env
chown root:root /etc/elyndor/elyndor.env

systemctl daemon-reload
systemctl enable elyndor
```

## 13. Production environment

Редактировать:

```bash
nano /etc/elyndor/elyndor.env
```

Шаблон:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080

ConnectionStrings__game="Host=127.0.0.1;Port=5432;Database=game;Username=elyndor;Password=CHANGE_ME"

Database__MigrateOnStartup=true
Content__RestorePublishedOnStartup=true
Content__AllowFileFallbackOnRestoreFailure=false
Frontend__DistPath=/opt/elyndor/current/frontend
AdminFrontend__DistPath=/opt/elyndor/current/frontend-admin

Authentication__Development__Enabled=false
Authentication__SigningKey=CHANGE_ME_AT_LEAST_32_BYTES
Authentication__Telegram__BotToken=CHANGE_ME

Administration__Telegram__Enabled=true
Administration__Telegram__WebhookSecret=CHANGE_ME_AT_LEAST_32_BYTES
Administration__Telegram__AllowedUserIds__0=CHANGE_ME
```

Для нескольких Telegram-администраторов использовать отдельный индекс:

```dotenv
Administration__Telegram__AllowedUserIds__0=111111111
Administration__Telegram__AllowedUserIds__1=222222222
Administration__Telegram__AllowedUserIds__2=333333333
```

Не использовать список через запятую в одном значении.

### Генерация секретов

Генерировать production secrets непосредственно на VPS:

```bash
openssl rand -base64 48
openssl rand -hex 32
```

Первое значение можно использовать для:

```text
Authentication__SigningKey
```

второе — для:

```text
Administration__Telegram__WebhookSecret
```

Telegram Bot Token берется из BotFather.

Ни один настоящий secret не должен попадать:

```text
в Git
в issue/PR
в документацию
в screenshots
в chat history
```

Проверить отсутствие placeholder'ов:

```bash
grep -n 'CHANGE_ME' /etc/elyndor/elyndor.env
```

При полностью заполненном файле команда ничего не выводит.

---

# Сборка production release

## 14. Где собирается приложение

Production release НЕ собирается на VPS.

Схема:

```text
GitHub Actions / developer machine
        |
        +-- npm ci
        +-- Vue build
        +-- dotnet publish Release
        +-- content package
        |
elyndor-linux-x64.tar.gz
        |
        v
production VPS
```

Это важно для текущего сервера с 1 GB RAM и небольшим диском.

## 15. GitHub Actions package-production

После того как нужные изменения прошли CI и слиты в `main`:

```text
GitHub
→ ElyndorMMORPG
→ Actions
→ package-production
→ Run workflow
→ Branch: main
→ Run workflow
```

Workflow создает artifact:

```text
elyndor-linux-x64
```

Внутри находится:

```text
elyndor-linux-x64.tar.gz
```

Artifact не содержит production secrets.

## 16. Локальная сборка на Linux при необходимости

```bash
./deploy/build-release.sh
```

По умолчанию архив:

```text
artifacts/elyndor-linux-x64.tar.gz
```

---

# Первый deploy и обновления

## 17. Передача release на VPS

Если архив скачан на Windows в Downloads:

```powershell
scp "$env:USERPROFILE\Downloads\elyndor-linux-x64.tar.gz" \
  root@194.226.97.122:/tmp/
```

Подключиться:

```powershell
ssh root@194.226.97.122
```

## 18. Deploy

```bash
elyndor-deploy /tmp/elyndor-linux-x64.tar.gz
```

Deployment script:

```text
1. проверяет archive paths
2. распаковывает release в /opt/elyndor/releases/<timestamp>
3. проверяет наличие backend + frontend
4. атомарно переключает /opt/elyndor/current
5. перезапускает elyndor.service
6. проверяет /api/v1/status
7. требует "status":"ready"
8. при ошибке возвращает предыдущий release
9. хранит несколько последних releases для rollback
```

Важно: в Production не использовать `/alive` как проверку backend. Health endpoints
`/health` и `/alive` намеренно не мапятся в Production; запрос может попасть в
Vue SPA fallback.

Правильная проверка:

```bash
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Ожидается:

```json
{"service":"Elyndor.Server","status":"ready",...}
```

## 19. Проверка после deploy

```bash
systemctl status elyndor --no-pager
curl -fsS http://127.0.0.1:5080/api/v1/status
ss -lntp | grep 5080
journalctl -u elyndor -n 100 --no-pager
```

Origin должен слушать только:

```text
127.0.0.1:5080
```

## 20. Как выкатывать каждое следующее обновление

Обычный update cycle:

```text
разработка
   |
branch
   |
PR
   |
CI green
   |
squash merge -> main
   |
package-production
   |
elyndor-linux-x64.tar.gz
   |
scp -> VPS
   |
elyndor-backup        (для серьезных обновлений)
   |
elyndor-deploy
   |
/api/v1/status ready
```

Команды на Windows:

```powershell
scp "$env:USERPROFILE\Downloads\elyndor-linux-x64.tar.gz" \
  root@194.226.97.122:/tmp/

ssh root@194.226.97.122
```

Команды на VPS:

```bash
elyndor-backup
elyndor-deploy /tmp/elyndor-linux-x64.tar.gz
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Для небольшой UI-only обновы backup БД не обязателен, но перед изменениями
с миграциями, контентом или важным gameplay state рекомендуется всегда делать
backup.

### Что сохраняется при обновлении

Release заменяет application files, но не PostgreSQL.

Сохраняются:

```text
accounts
characters
inventory
equipment
talents
gold
world state persisted in DB
admin data
content revisions
published releases
audit history
```

При:

```dotenv
Database__MigrateOnStartup=true
```

новые EF Core migrations применяются при старте приложения.

---

# Backup и rollback

## 21. Ручной backup PostgreSQL

```bash
elyndor-backup
```

По умолчанию dump сохраняется:

```text
/var/backups/elyndor/game-YYYYMMDDTHHMMSSZ.dump
```

Формат — PostgreSQL custom format.

Проверка:

```bash
ls -lh /var/backups/elyndor/
```

Локальный backup на том же VPS удобен для быстрого восстановления, но не должен
быть единственной копией важных данных. Периодически копировать dump на другой
хост или внешнее storage.

## 22. Автоматический application rollback

`elyndor-deploy` сам возвращает предыдущий `current`, если новый backend не
выходит в состояние `ready`.

Проверить releases:

```bash
ls -lah /opt/elyndor/releases
readlink -f /opt/elyndor/current
```

## 23. Ручной rollback application release

Если требуется вручную вернуть release:

```bash
ls -1dt /opt/elyndor/releases/*
```

Выбрать нужный каталог и:

```bash
ln -sfn /opt/elyndor/releases/RELEASE_ID /opt/elyndor/current
systemctl restart elyndor
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Это откатывает application binaries/content bundle, но не делает downgrade
PostgreSQL schema. Для несовместимых DB migrations нужен отдельный план
восстановления БД.

## 24. Восстановление DB dump

Остановить приложение:

```bash
systemctl stop elyndor
```

Для полного восстановления production DB сначала сделать дополнительный backup.

Пример восстановления custom dump в заранее подготовленную пустую базу:

```bash
sudo -u postgres dropdb --if-exists game
sudo -u postgres createdb --owner=elyndor game
sudo -u postgres pg_restore \
  --dbname=game \
  --no-owner \
  --role=elyndor \
  /var/backups/elyndor/game-YYYYMMDDTHHMMSSZ.dump
```

Запустить:

```bash
systemctl start elyndor
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Не выполнять destructive restore без подтвержденного backup.

---

# DNS и HTTPS

## 25. DNS для Elyndor

Используется:

```text
Domain: elyndor.su
DNS:    ns1.reg.ru
        ns2.reg.ru
```

Production Mini App:

```text
game.elyndor.su
```

Resource records:

```text
A  @     -> 194.226.97.122
A  game   -> 194.226.97.122
A  admin  -> 194.226.97.122
```

Корневой домен можно использовать позже под landing page.

Проверка authoritative REG.RU DNS:

```powershell
nslookup game.elyndor.su ns1.reg.ru
nslookup admin.elyndor.su ns1.reg.ru
```

Ожидается:

```text
194.226.97.122
```

Проверка публичного DNS:

```powershell
nslookup -type=ns elyndor.su 1.1.1.1
nslookup game.elyndor.su 1.1.1.1
nslookup admin.elyndor.su 1.1.1.1
```

Перед выпуском public TLS публичный resolver должен возвращать:

```text
game.elyndor.su -> 194.226.97.122
```

## 26. Установка Caddy

```bash
apt install -y debian-keyring debian-archive-keyring apt-transport-https curl

curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
  | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg

curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
  | tee /etc/apt/sources.list.d/caddy-stable.list

chmod o+r /usr/share/keyrings/caddy-stable-archive-keyring.gpg
chmod o+r /etc/apt/sources.list.d/caddy-stable.list

apt update
apt install -y caddy
```

## 27. Caddy config

```bash
cat >/etc/caddy/Caddyfile <<'EOF'
game.elyndor.su {
    encode zstd gzip

    @internalAdmin path /__admin*
    respond @internalAdmin 404

    reverse_proxy 127.0.0.1:5080
}

admin.elyndor.su {
    encode zstd gzip

    handle /api/* {
        reverse_proxy 127.0.0.1:5080
    }

    handle /hubs/* {
        reverse_proxy 127.0.0.1:5080
    }

    handle {
        rewrite * /__admin{uri}
        reverse_proxy 127.0.0.1:5080
    }
}
EOF
```

Проверить:

```bash
caddy validate --config /etc/caddy/Caddyfile
systemctl reload caddy
systemctl status caddy --no-pager
```

Caddy автоматически управляет TLS certificate, если:

```text
game.elyndor.su -> public VPS IP
80/tcp доступен
443/tcp доступен
```

Логи:

```bash
journalctl -u caddy -n 100 --no-pager
```

## 28. Проверка public HTTPS

```bash
curl -I https://game.elyndor.su/
curl -fsS https://game.elyndor.su/api/v1/status
```

Ожидается backend response со:

```json
"status":"ready"
```

После этого открыть:

```text
https://game.elyndor.su
```

в обычном браузере и проверить загрузку UI.

---


## 30A. Отдельный Admin V2 в браузере

Admin V2 публикуется вместе с обычным production release:

```text
frontend/       -> game.elyndor.su
frontend-admin/ -> admin.elyndor.su
```

Начиная с Admin V2 production package, Elyndor.Server автоматически находит
`frontend` и `frontend-admin` рядом с опубликованным server binary. Явные
`Frontend__DistPath` / `AdminFrontend__DistPath` остаются поддерживаемыми и
имеют приоритет, но старый production env не требуется переписывать только ради
Admin V2.

DNS:

```text
A admin -> 194.226.97.122
```

После публичного распространения DNS:

```powershell
nslookup admin.elyndor.su 1.1.1.1
```

должен вернуть:

```text
194.226.97.122
```

После deploy проверить:

```bash
curl -fsS https://admin.elyndor.su/api/v1/status
curl -I https://admin.elyndor.su/
```

Открыть в обычном браузере:

```text
https://admin.elyndor.su
```

Авторизация Admin V2 не зависит от Telegram WebView:

```text
Telegram ID
    ↓
server-side admin allowlist
    ↓
Elyndor Bot sends one-time 6-digit code
    ↓
browser verifies code
    ↓
short-lived SUPER_ADMIN JWT in tab memory
```

Код одноразовый, действует 5 минут и имеет ограничения на повторные запросы и
неверные попытки. Для получения сообщения администратор должен ранее открыть
Elyndor Bot в Telegram.

### Временный break-glass вход при недоступности Telegram

Если production VPS временно не может установить исходящее соединение с Telegram Bot API,
можно явно включить резервный вход по паролю. Это аварийный режим, а не замена Telegram
авторизации.

В `/etc/elyndor/elyndor.env` добавить:

```dotenv
Administration__WebAuthentication__EmergencyPasswordEnabled=true
Administration__WebAuthentication__EmergencyPassword=<LONG_RANDOM_PASSWORD>
```

Пароль должен быть не короче 20 UTF-8 байт. Он не коммитится в Git и не должен попадать
в issue, PR, screenshots или chat history. После изменения:

```bash
systemctl restart elyndor
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Резервный вход всё равно требует Telegram ID из
`Administration__Telegram__AllowedUserIds`. После пяти неверных попыток этот ID блокируется
для password-login на 5 минут в текущем процессе сервера. Когда доступ до Telegram восстановлен,
выключить аварийный режим:

```dotenv
Administration__WebAuthentication__EmergencyPasswordEnabled=false
Administration__WebAuthentication__EmergencyPassword=
```

и снова перезапустить `elyndor`.

Игровой host специально блокирует внутренний static route `/__admin`, поэтому
Admin JS не доступен как обычная часть `game.elyndor.su`.

# Telegram

## 29. Telegram Mini App URL

После успешной HTTPS-проверки изменить Menu Button через BotFather:

```text
@BotFather
→ /mybots
→ Elyndor
→ Bot Settings
→ Menu Button
→ Configure menu button
```

Production URL:

```text
https://game.elyndor.su
```

Старый Tailscale Funnel URL после перехода на VPS больше не используется
игроками.

## 30. Telegram webhook

Production endpoint:

```text
https://game.elyndor.su/api/v1/administration/telegram/webhook
```

Bot token и webhook secret хранятся только в:

```text
/etc/elyndor/elyndor.env
```

Не выводить secret values в shell history и не публиковать их.

После изменения production secrets:

```bash
systemctl restart elyndor
curl -fsS http://127.0.0.1:5080/api/v1/status
```

---

# Перенос локальной БД

## 31. Миграция данных с Aspire PostgreSQL

Если требуется перенести существующих development characters/accounts/content,
используется:

```text
local PostgreSQL
      |
   pg_dump
      |
 custom dump
      |
     scp
      |
production PostgreSQL 18
      |
  pg_restore
```

До переноса:

1. остановить запись в локальную БД;
2. сделать dump;
3. сделать production backup, если production уже содержит данные;
4. остановить `elyndor.service`;
5. восстановить dump;
6. запустить Elyndor;
7. проверить персонажа, inventory, talents, published content и admin state;
8. не удалять локальный Aspire volume до полной проверки production.

---

# Повседневная эксплуатация

## 32. Основные команды

Статус:

```bash
systemctl status elyndor --no-pager
systemctl status postgresql --no-pager
systemctl status caddy --no-pager
```

Restart:

```bash
systemctl restart elyndor
```

Backend status:

```bash
curl -fsS http://127.0.0.1:5080/api/v1/status
```

Public status:

```bash
curl -fsS https://game.elyndor.su/api/v1/status
```

Logs:

```bash
journalctl -u elyndor -n 200 --no-pager
journalctl -u caddy -n 100 --no-pager
tail -n 100 /var/log/postgresql/postgresql-18-main.log
```

Resources:

```bash
free -h
swapon --show
df -h /
ps aux --sort=-%mem | head
```

Listeners:

```bash
ss -lntp
```

Backups:

```bash
elyndor-backup
ls -lh /var/backups/elyndor/
```

## 33. Что проверять после reboot VPS

```bash
systemctl is-active postgresql
systemctl is-active elyndor
systemctl is-active caddy

curl -fsS http://127.0.0.1:5080/api/v1/status
curl -fsS https://game.elyndor.su/api/v1/status
```

Все три services должны быть active.

## 34. Проверка диска

На Free Tier диск маленький, поэтому периодически:

```bash
df -h /
du -sh /opt/elyndor /var/lib/postgresql /var/backups/elyndor /var/log
```

Deployment script сохраняет ограниченное количество последних application
releases. Backup helper по умолчанию удаляет старые dumps согласно retention.

---

# Incident checklist

## 35. Elyndor не запускается

```bash
systemctl status elyndor --no-pager
journalctl -u elyndor -n 200 --no-pager
curl -v http://127.0.0.1:5080/api/v1/status
```

Проверить:

```bash
systemctl status postgresql --no-pager
ss -lntp | grep 5432
grep -n 'CHANGE_ME' /etc/elyndor/elyndor.env
```

Не удалять PostgreSQL data directory/volume как первый способ исправления.

## 36. Домен не открывается

С Windows:

```powershell
nslookup game.elyndor.su ns1.reg.ru
nslookup game.elyndor.su 1.1.1.1
```

Если authoritative REG.RU возвращает IP, а public resolver дает NXDOMAIN,
проблема на уровне DNS propagation/delegation, а не Elyndor.

## 37. HTTPS не выпускается

```bash
journalctl -u caddy -n 100 --no-pager
caddy validate --config /etc/caddy/Caddyfile
ufw status verbose
```

Проверить, что public DNS уже возвращает `194.226.97.122`.

## 38. Backend работает локально, но через домен нет

```bash
curl -fsS http://127.0.0.1:5080/api/v1/status
curl -v https://game.elyndor.su/api/v1/status
systemctl status caddy --no-pager
```

Если первый запрос работает, проблема находится между Caddy/DNS/TLS, а не в
ASP.NET/Core/PostgreSQL.

## 39. Нехватка памяти

```bash
free -h
swapon --show
journalctl -k | grep -i -E 'oom|out of memory'
```

На текущем Free Tier должен быть активен 1 GiB swap.

Не запускать на production VPS одновременно Node build, .NET SDK build, Aspire
или Docker build.

---

# Security rules

## 40. Никогда не коммитить

```text
Telegram Bot Token
JWT Signing Key
Telegram Webhook Secret
PostgreSQL password
/etc/elyndor/elyndor.env
private SSH key
REG.RU API/Terraform token
```

API/Terraform token REG.RU не является SSH credential и не нужен Elyndor runtime.

## 41. Production authentication

Обязательные настройки:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
Authentication__Development__Enabled=false
Content__AllowFileFallbackOnRestoreFailure=false
```

Production не должен использовать development Telegram identity.

---

# Git / release policy

## 42. Правильный путь изменения production

```text
short-lived branch
      |
      v
     PR
      |
      v
full CI green
      |
      v
squash merge
      |
      v
     main
      |
      v
package-production
      |
      v
production deploy
```

Не выкатывать случайный local working tree напрямую на production.

## 43. Минимальная памятка обновления

Если всё уже настроено, каждое обновление сводится к:

```text
1. merge проверенного PR в main
2. Actions -> package-production -> Run workflow
3. скачать elyndor-linux-x64.tar.gz
4. scp archive на VPS
5. elyndor-backup (если update серьезный)
6. elyndor-deploy archive
7. проверить /api/v1/status
8. проверить https://game.elyndor.su
```

Команды:

```powershell
scp "$env:USERPROFILE\Downloads\elyndor-linux-x64.tar.gz" root@194.226.97.122:/tmp/
ssh root@194.226.97.122
```

```bash
elyndor-backup
elyndor-deploy /tmp/elyndor-linux-x64.tar.gz
curl -fsS http://127.0.0.1:5080/api/v1/status
curl -fsS https://game.elyndor.su/api/v1/status
```

Это текущий основной production workflow Elyndor.
