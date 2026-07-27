# Container health checks

A health check runs **inside** the container it monitors. It can therefore only
use tools that image actually contains — and production images increasingly
contain almost nothing.

This is not a theoretical concern. It cost CloudFilm two debugging rounds: the
Orthanc check called `curl`, `orthancteam/orthanc` has no `curl`, so the check
could never pass, and `ohif` — which waits on `depends_on: service_healthy` —
never started. Orthanc itself was working perfectly the entire time.

---

## Why `curl` is forbidden

`curl` is not part of a minimal base image, and adding it means adding a package
to a production image for the sole benefit of a health check. That is a bad
trade: a larger attack surface and a slower build, in exchange for something the
image can usually already do.

Confirmed absent from the images CloudFilm uses:

- **`orthancteam/orthanc`** — observed at runtime: `/bin/sh: 1: curl: not found`.
- **`mcr.microsoft.com/dotnet/aspnet`** — Microsoft excludes `curl` from the
  .NET Linux images by design; there is no `wget` either.

**Never `apt-get install curl` to make a health check work.** If a probe needs a
package the image does not have, the probe is wrong, not the image.

---

## How to choose a probe

In order. Stop at the first one that applies.

### 1. An official probe shipped by the image

Always preferred. It is maintained by the people who build the image, and it
knows how the software is configured.

Orthanc ships `/probes/test-aliveness.py`, added for exactly this purpose.

### 2. A native tool the image is defined by

The database client in a database image, the runtime in a runtime image. These
cannot go missing without the image ceasing to be what it is.

`pg_isready` in `postgres`. `redis-cli` in `redis`. `node` in `node`.

Prefer the tool the image *is* over a tool that merely happens to be present.
The portal previously used busybox `wget`, which is an Alpine implementation
detail — switching the base tag to `node:22-slim`, which is Debian and ships
neither `wget` nor `curl`, would have broken it silently. `node` cannot break
that way.

### 3. A real HTTP request built from shell primitives

When the image has no HTTP client but does have a shell, bash can open a socket
directly:

```bash
exec 3<>/dev/tcp/127.0.0.1/8080 \
  && printf 'GET /health/live HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 \
  && head -n 1 <&3 | grep -q ' 200 '
```

`bash`, `printf`, `head` and `grep` are Essential packages in Debian and Ubuntu:
present unless someone has deliberately removed them. This asserts a `200` from
the real endpoint, so it is a true health check.

### 4. A bare TCP connect — last resort only

```bash
timeout 3 bash -c '</dev/tcp/127.0.0.1/8080'
```

Use only when the service exposes no HTTP health endpoint. It proves a port is
open, which is much weaker: a process can accept connections while its database
is unreachable and every request is failing.

### Not an option

Adding a package. Deleting the health check to make a dependency start. A check
that always exits 0 is worse than no check at all — it makes an unhealthy
service look healthy, and `depends_on` will happily proceed.

---

## What CloudFilm uses, and why

| Service | Image | Probe | Tier | Why it works |
| --- | --- | --- | --- | --- |
| `orthanc` | `orthancteam/orthanc` | `python3 /probes/test-aliveness.py` | 1 | The image's own probe, shipped since 21.10.0. Reads port, scheme and credentials from Orthanc's configuration, so it is correct whether authentication is on or off. Requests `/changes`, which touches the database, so healthy means the PostgreSQL index is reachable — not merely that the port is open. |
| `postgres` | `postgres:17-alpine` | `pg_isready -h 127.0.0.1 …` | 2 | `pg_isready` ships with the image. `-h 127.0.0.1` is load-bearing: during first-run initialisation the entrypoint starts PostgreSQL with `listen_addresses=''`, so a Unix-socket probe reports healthy while `/docker-entrypoint-initdb.d` scripts are still running — and Orthanc would start before its role exists. Forcing TCP keeps it unhealthy until initialisation has genuinely finished. |
| `redis` | `redis:7-alpine` | `redis-cli ping` | 2 | The image's own client. Exits non-zero when the server does not answer. |
| `portal` | `node:22-alpine` | `node -e "fetch(…)"` | 2 | `node` is the binary the image exists for. Global `fetch` has been available since Node 18. Asserts `response.ok`, so a 500 is reported unhealthy. |
| `api` | `aspnet:9.0-noble` | bash `/dev/tcp` HTTP request | 3 | No HTTP client in the image and none added. Uses only Essential packages, and asserts `200` from `/health/live`. |
| `retrieve` | `aspnet:9.0-noble` | bash `/dev/tcp` HTTP request | 3 | As above. |
| `ohif` | `ohif/app` | none | — | Nothing depends on it, so an unverified probe would add risk without benefit. The portal already determines viewer availability from the browser, which is the only vantage point that matters. |

> **Do not move `api` or `retrieve` to a `-chiseled` tag** without replacing
> their probes first. Chiselled images have no shell at all, so tier 3 stops
> working. That would need a health-check binary published with the app.

---

## `depends_on: service_healthy`

Every `service_healthy` edge is a promise that the target's health check *can*
pass. When it cannot, the dependant never starts and the symptom appears in the
wrong place entirely — the failure looked like "OHIF is broken" when OHIF had
never been started.

Current graph, all reachable:

```
postgres ─► orthanc ─► retrieve ─► api ─► portal
    └────────────────────────────► api
redis ──────────────────────────► api
         orthanc ─► ohif
```

Development and production gate identically. The development overlay used to
downgrade `retrieve` and `ohif` to `service_started` to work around the broken
Orthanc probe; that has been removed, because masking a broken health check is
what allowed the real fault to go unnoticed. To start one service anyway while
debugging:

```bash
docker compose up -d --no-deps ohif
```

---

## Verifying a probe before committing it

Run the exact command outside Docker, against something that answers, and
against something that does not. A probe that has only been tested against a
healthy service is half tested — the case that matters is the failing one.

```bash
# extract precisely what Docker will run, so YAML escaping is tested too
python3 -c "
import yaml
print(yaml.safe_load(open('docker-compose.yml'))['services']['api']['healthcheck']['test'])"
```

Then confirm in the running stack:

```bash
docker inspect --format '{{json .State.Health}}' cloudfilm-api-1 | python3 -m json.tool
```

`Status` must reach `healthy`, and `Log[].ExitCode` must be `0`. A non-zero
`ExitCode` with `Output` containing `not found` is this whole document's failure
mode repeating itself.
