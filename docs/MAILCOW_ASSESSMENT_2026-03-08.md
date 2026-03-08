# Mailcow Assessment - 2026-03-08

## Executive Summary

The current Ubuntu host at `192.168.1.10` is not a safe production target for `mailcow` yet.

The blocker is not a single bad password or a missing certificate. The host currently has:

- a live mail stack already bound to the public mail ports
- a live web stack already bound to `80/443`
- a partially bootstrapped `mailcow` deployment created with stale container environment values
- a compose/project mismatch that prevents clean incremental recovery

Because of that, the correct next step is a controlled migration plan, not a forced restart.

## What Was Confirmed

### Existing mail services are already occupying the ports that Mailcow needs

The host is already serving mail through:

- `exim4` on `25`, `465`, `587`
- `dovecot` on `110`, `143`, `993`, `995`

That means a production `mailcow` deployment cannot bind its own SMTP/IMAP/POP services without replacing the current mail stack.

### Existing web services are already occupying the ports that Mailcow needs

The host is already serving web traffic through:

- `nginx`
- `apache`

Mailcow also expects to own `80/443` for its web UI and ACME flow unless it is explicitly placed behind a reverse proxy.

### Mailcow's rendered configuration is correct, but the current containers were created with empty runtime values

Observed state:

- `mailcow.conf` contains populated values for `DBROOT`, `DBPASS`, and `REDISPASS`
- `docker compose config` renders those values correctly into the service definitions
- `docker inspect` on the current `mysql` and `redis` containers shows those environment variables as empty

This means the currently created containers are stale and were instantiated from an earlier broken state.

### The current MariaDB and Redis failures are real bootstrap failures

Observed logs:

- MariaDB: `Database is uninitialized and password option is not specified`
- Redis: `requirepass` with wrong number of arguments

These failures are consistent with containers created with empty environment values.

### The Mailcow compose project is inconsistent

Existing resources use the project name:

- `mailcow-dockerized`

During repair attempts, the compose runtime also tried to resolve resources under:

- `mailcowdockerized`

That mismatch caused network recreation attempts and prevented a clean incremental restart.

## Current Container State

At the time of assessment, the Mailcow stack was only partially running:

- running: `php-fpm`, `sogo`, `memcached`, `clamd`, `postfix-tlspol`, `olefy`, `netfilter`
- failing or missing bootstrap path: `mysql`, `redis`, `dockerapi`, `unbound`
- not started: `nginx`, `postfix`, `dovecot`, `rspamd`, `acme`, `watchdog`, `ofelia`

In practice, this means Mailcow is not usable as a mail platform on this host right now.

## Why This Must Not Be Forced Into Production

If Mailcow is forced into this server without a migration window, the likely outcomes are:

- conflict with the currently working mail stack
- conflict with the current web stack
- broken ACME/TLS automation
- ambiguous ownership of `80/443`
- split-brain mail behavior between `exim4/dovecot` and `postfix/dovecot` from Mailcow

That is not an acceptable production state.

## Recommended Paths

### Option A - Dedicated mail host or VM

This is the recommended production path.

Use a dedicated host for Mailcow and move:

- DNS `MX`
- `autodiscover`
- `autoconfig`
- SMTP/IMAP/POP
- TLS

to that dedicated environment.

This is the lowest-risk option.

### Option B - Planned cutover on the current host

Only do this with a maintenance window.

Required steps:

1. Inventory and preserve the current mail setup.
2. Stop and disable the active host mail services:
   - `exim4`
   - `dovecot`
   - related antispam/antivirus services that are part of the old stack
3. Decide whether Mailcow will own `80/443` directly or sit behind a reverse proxy.
4. Clean the broken Mailcow project state.
5. Rebuild Mailcow from a single, explicit compose project name.
6. Re-run certificate issuance and validate mail ports.
7. Only then switch DNS and MX records.

### Option C - Lab mode only

If the goal is only to evaluate the Mailcow UI and internals, it can be rebuilt behind alternate web ports and without taking over production mail traffic.

That is useful for testing, but it is not a production deployment.

## Immediate Recommendation

Do not continue with production Mailcow activation on this Ubuntu host today.

Instead:

1. Keep the current host mail stack alive.
2. Decide between:
   - dedicated Mailcow host
   - maintenance-window migration on this host
3. If desired, build a lab Mailcow instance on alternate ports first.

## External References

- Mailcow Dockerized reverse proxy guidance:
  `https://docs.mailcow.email/post_installation/reverse-proxy/r_p/`
- Mailcow older prerequisites page noting that conflicting web and mail services should be removed first:
  `https://mailcow.github.io/mailcow/Getting-started/Pr-install/`

