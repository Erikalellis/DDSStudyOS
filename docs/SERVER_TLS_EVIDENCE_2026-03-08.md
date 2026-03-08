# Server TLS Evidence - 2026-03-08

## Scope

Enable HTTPS on the public AWS ingress for:

- `deepdarkness.com.br`
- `www.deepdarkness.com.br`

and keep DDS StudyOS reachable through:

- `https://deepdarkness.com.br/studyos/`

## Changes Applied

### AWS host

- replaced the ad-hoc `nginx` site layout with a consolidated root-domain proxy
- kept compatibility for:
  - `/`
  - `/studyos/`
  - `/api`
  - `/admin`
  - `/aluno`
  - `/professor`
- disabled the old `dds` and `dds-tunnel` site symlinks

### Certbot

Issued certificate with:

- `deepdarkness.com.br`
- `www.deepdarkness.com.br`

Result:

- certificate path: `/etc/letsencrypt/live/deepdarkness.com.br/fullchain.pem`
- key path: `/etc/letsencrypt/live/deepdarkness.com.br/privkey.pem`
- expiry: `2026-06-06`

## Validation

External checks passed:

- `GET https://deepdarkness.com.br/` => `200`
- `GET https://www.deepdarkness.com.br/` => `200`
- `GET https://deepdarkness.com.br/studyos/` => `200`

Socket checks on AWS host:

- `0.0.0.0:80` => listening by `nginx`
- `0.0.0.0:443` => listening by `nginx`
- `[::]:80` => listening by `nginx`
- `[::]:443` => listening by `nginx`

## Pending

- `studyos.deepdarkness.com.br` still has no DNS record in Route 53
- because of that, TLS for the dedicated subdomain was not issued yet

## Current Canonical Public URL for DDS StudyOS

- portal: `https://deepdarkness.com.br/studyos/`
- catalog feed: `https://deepdarkness.com.br/studyos/api/catalog`
- health: `https://deepdarkness.com.br/studyos/healthz`

