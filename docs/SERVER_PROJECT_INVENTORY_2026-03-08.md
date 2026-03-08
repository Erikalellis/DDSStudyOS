# Server Project Inventory - 2026-03-08

## Objective

This document is the operational inventory of the current DDS server environment.

It exists to answer four questions clearly:

1. what is running
2. where it is running
3. what is public today
4. what still needs migration or hardening

## Infrastructure Topology

### Public entrypoint

- public host: `177.71.165.60`
- public domains:
  - `https://deepdarkness.com.br/`
  - `https://www.deepdarkness.com.br/`
  - `https://deepdarkness.com.br/studyos/`

### Internal application host

- Ubuntu server: `192.168.1.10`
- primary user: `kika`
- project root on host: `~/dds-projetos`

### Traffic model

- public traffic terminates on the AWS host with `nginx`
- local apps on the Ubuntu server are exposed to AWS through reverse SSH tunnels
- DDS StudyOS portal is served from local Docker on `127.0.0.1:5081`

## Public Routes Confirmed

### Active and working

| Public URL | Current target | Purpose |
|---|---|---|
| `https://deepdarkness.com.br/` | local `8080` via AWS `nginx` | DDS root site |
| `https://www.deepdarkness.com.br/` | same as root | DDS root site |
| `https://deepdarkness.com.br/studyos/` | local `5081` via AWS `nginx` | DDS StudyOS portal |
| `https://deepdarkness.com.br/studyos/api/catalog` | local `5081` | DDS StudyOS catalog feed |
| `https://deepdarkness.com.br/studyos/healthz` | local `5081` | DDS StudyOS portal health |

### Pending

| Public URL | Status | Blocker |
|---|---|---|
| `studyos.deepdarkness.com.br` | not created | Route 53 record missing |

## Docker Projects on Ubuntu

### Running

| Container | Purpose | Local binding | Exposure status |
|---|---|---:|---|
| `ddsstudyos-portal` | DDS StudyOS portal | `127.0.0.1:5081 -> 8080` | published through AWS `nginx` |
| `ad-app-backend` | Academia backend | `0.0.0.0:8000` | still exposed/raw |
| `ad-app-admin-panel` | Academia admin | `0.0.0.0:3001` | still exposed/raw |
| `ad-app-professor-dashboard` | Academia professor | `0.0.0.0:3002` | still exposed/raw |
| `ad-app-student-portal` | Academia student | `0.0.0.0:3003` | still exposed/raw |
| `dds-filebrowser` | file management | `0.0.0.0:8585` | still exposed/raw |
| `open-webui` | AI UI | `0.0.0.0:3000` | still exposed/raw |
| `taskingai-db-1` | database | internal only | not public |
| `taskingai-cache-1` | redis | internal only | not public |

### Removed from active runtime

| Stack | Action taken | Date |
|---|---|---|
| `mailcow-dockerized` | containers and network removed, volumes preserved | 2026-03-08 |

## System Services on Ubuntu

| Service | Port | Role | Status |
|---|---:|---|---|
| `dds-static-server.service` | `8080` | DDS root static site | active |
| `dds-ledm.service` | `5000` | LEDM app | active |
| `dds-monitor.service` | `1984` | DDS monitor | active |
| `server-monitor.service` | `3008` | monitor API | active |
| `dds-tunnel.service` | n/a | reverse tunnels to AWS | active |
| `nginx.service` | `80/443` local host web | active |
| `apache` related services | internal legacy web components | active |
| `exim4.service` | `25/465/587` | current mail stack | active |
| `dovecot.service` | `110/143/993/995` | current mail stack | active |
| `spamd.service` | spam filtering | active |
| `clamav-daemon.service` | antivirus | active |
| `hestia.service` | legacy control panel | active |

## Legacy Web Domains Still Present on Ubuntu

Under `/home/erikalellis/web`:

- `deepdarkness.duckdns.org`
- `ddscreate.duckdns.org`
- `ddsfit.duckdns.org`
- `ddsmidiawiki.duckdns.org`
- `forumdds.duckdns.org`

These should be treated as legacy workloads until individually reviewed.

## Security and Exposure Notes

### Good state

- DDS StudyOS portal itself is bound locally on `127.0.0.1:5081`
- AWS `nginx` is now serving the root domain over HTTPS
- `mailcow` partial stack was removed before it could interfere with production

### Still needs hardening

Several reverse SSH tunnels are still exposing raw ports on the AWS host.

These should be changed from:

- `-R PORT:localhost:PORT`

to:

- `-R 127.0.0.1:PORT:localhost:PORT`

Priority hardening targets:

- `3000`
- `3001`
- `3002`
- `3003`
- `5000`
- `8000`
- `8585`
- `1984`
- `8083`

## Immediate Next Actions

1. Create Route 53 DNS for `studyos.deepdarkness.com.br`.
2. After DNS exists, add subdomain `nginx` server block and issue its TLS certificate.
3. Move the raw Academia/AI/FileBrowser services behind `nginx` subdomains.
4. Harden the reverse SSH bindings to `127.0.0.1`.
5. Keep mail as a separate project; do not mix it with the DDS StudyOS rollout.

## DDS StudyOS Status

The DDS StudyOS baseline is closed for the day as a public initial release baseline:

- app version line: `3.2.8`
- public update channel: `DDSStudyOS-Updates`
- public portal endpoint: `https://deepdarkness.com.br/studyos/`
- store/catalog source: `https://deepdarkness.com.br/studyos/api/catalog`

