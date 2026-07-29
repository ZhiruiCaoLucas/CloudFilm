# CloudFilm engineering review

Scope: Docker, .NET configuration and start-up, PostgreSQL, Orthanc, OHIF,
Portal. Architecture unchanged — every fix below is a correction, not a
redesign.

> **Verification status.** Issues 1–4 and 12 were confirmed by runtime evidence
> from the operator. Everything else was proven by inspection or by executing
> the mechanism outside Docker. **Nothing in this repository has been observed
> running as a complete stack**, because the environment this review was
> performed in has no Docker daemon, no root, and no container registry access.
> Section "Not verified" lists exactly what remains open. No claim of a
> successful `docker compose up` is made here.

---

## Issues found and fixed

### 1. Orthanc health check called `curl`, which the image does not contain
**Confirmed at runtime.** `/bin/sh: 1: curl: not found`.

Orthanc was starting perfectly — HTTP listening on 8042, PostgreSQL, DICOMWeb
and Explorer2 plugins all loaded — but the health check could never pass. Since
`ohif` waits on `depends_on: condition: service_healthy`, **the viewer never
started at all**, and the symptom appeared as "OHIF is broken".

Fixed: `python3 /probes/test-aliveness.py`, the probe shipped inside the image
since 21.10.0. It reads port, scheme and credentials from Orthanc's own
configuration, so it is correct with authentication on or off, and it requests
`/changes`, which exercises the database rather than only the HTTP port.

### 2. The Orthanc database role was created with the wrong password
`scripts/init-databases.sh` runs inside the **postgres** container and read
`ORTHANC__POSTGRESQL__PASSWORD` — a variable defined only on the **orthanc**
service. It was therefore always unset there, the `:-orthanc` fallback won
silently, and the role was created with a password Orthanc never uses. `set -eu`
did not catch it: `${VAR:-default}` is explicitly the "do not fail on unset"
form.

Fixed: both services receive `ORTHANC_DB_PASSWORD`, guarded with `:?` so a
missing value fails at compose parse. The script has no default at all and sets
the password on every run, so re-running repairs drift.

**Migration note:** `/docker-entrypoint-initdb.d` scripts run only when the data
directory is empty. An existing volume still holds the old password — run
`docker compose ... down -v`, or `ALTER ROLE orthanc WITH LOGIN PASSWORD …`.

### 3. Two more health checks called `curl`
`api` and `retrieve` had the identical defect: Microsoft excludes `curl` from
the .NET runtime images, and there is no `wget` either. `portal` waits on `api`,
so this would have blocked the portal the moment Orthanc was fixed.

Fixed: an HTTP request over bash's `/dev/tcp`, asserting `200` from
`/health/live`, using only `bash`, `printf`, `head` and `grep` — Essential
packages in the Ubuntu base these images are built on. No package installed.
This is a real health check, not a TCP connect: verified to reject a 503.

> Do not move `api` or `retrieve` to a `-chiseled` tag without replacing these.
> Chiselled images have no shell.

### 4. The portal health check relied on an Alpine implementation detail
It used busybox `wget`. Switching the base to `node:22-slim`, which is Debian
and ships neither `wget` nor `curl`, would have broken it silently.

Fixed: `node -e "fetch(…)"`. `node` is the binary the image exists for.

### 5. `pg_isready` reported healthy during database initialisation
On first run the postgres entrypoint starts the server with
`listen_addresses=''` — Unix socket only — while `/docker-entrypoint-initdb.d`
scripts run. A socket probe therefore reports healthy before the `orthanc` role
exists, so Orthanc could start against a database that is not ready for it.

Fixed: `pg_isready -h 127.0.0.1` forces TCP, which is closed during that window.

### 6. The development overlay masked the broken health check
It downgraded `retrieve` and `ohif` to `service_started`. That workaround is why
the Orthanc fault went unnoticed. Removed — development and production now gate
identically, so a broken probe fails visibly in development first.

### 7. `:latest` on both third-party images
`orthancteam/orthanc:latest` and `ohif/app:latest`. An image that changes
underneath a clinical deployment is neither reproducible nor auditable after an
incident.

Fixed: pinned to `orthancteam/orthanc:25.4.2` and `ohif/app:v3.6.1`. The `v`
prefix is part of OHIF's tag format — `ohif/app:3.6.1` does not exist. All other
images were already version-pinned (`postgres:17-alpine`, `redis:7-alpine`,
`node:22-alpine`, `mcr.microsoft.com/dotnet/{aspnet,sdk}:9.0-noble`).

### 8. The OHIF proxy forwarded a header the browser never sends
`services/ohif/default.conf` contained `proxy_set_header Authorization
$http_authorization` under a comment claiming it attached Orthanc credentials.
It attached nothing, and would have produced a confident 401.

Fixed: removed, with the real requirement documented — a deployment with
authentication on must inject a Basic credential from a secret at that hop.
Local development runs Orthanc with authentication disabled instead.

### 9. `Cross-Origin-Embedder-Policy` on the viewer bought nothing
COEP is what OHIF needs for `SharedArrayBuffer`, but cross-origin isolation
requires the **whole chain** — the portal would have to send COOP and COEP too,
and every OHIF subresource would need CORP. Set on the viewer alone it achieves
nothing and can block resources. A plausible blank-page cause.

Fixed: removed, with the condition for re-enabling documented. 2D review — pan,
zoom, window/level — does not need it.

### 10. OHIF's study list was disabled
`showStudyList: false` meant a deep link to a study Orthanc does not hold
produced a blank screen with no route out.

Fixed: enabled as a fallback. The workspace still opens studies by URL.

### 11. The BuildKit cache mount cached nothing
The portal Dockerfile mounted a cache on `/pnpm/store`, but pnpm does not place
its store under `PNPM_HOME` — it chooses a directory on the project's
filesystem. Verified: `pnpm store path` resolves to `/tmp/.pnpm-store/v11` for a
project in `/tmp`. Every build re-downloaded the full dependency tree.

Superseded by issue 12.

### 12. The portal Docker build failed while the host succeeded
**Confirmed fixed at runtime by the operator.**

Replaced with the standard pnpm pattern: pnpm installed explicitly with npm
rather than relying on corepack to fetch the pinned version at build time; the
separate deps stage, the cache mount and cross-stage `node_modules` copying all
removed; `CI=true` and `COREPACK_ENABLE_DOWNLOAD_PROMPT=0` so nothing can wait
on a prompt. Next.js standalone output and the runtime stage are unchanged.

### 13. The build context included sample DICOM
The simplified Dockerfile copies the whole context, so a seeded CT study —
hundreds of megabytes — would be sent to the daemon on every build.

Fixed: `services/orthanc/seed` added to `.dockerignore`.

### 14. Portal configuration failed in the wrong direction
A missing variable threw on module load, so `pnpm dev` could not start without
two environment files. Worse, in production the throw happened lazily on first
request: the container **passed its liveness probe, was marked healthy, and
then returned 500 for every page**.

Fixed: development and build fall back to the documented local port map with a
warning; production runtime still refuses to start, and `instrumentation.ts`
forces that check at start-up so the process exits 1 instead of serving errors.

### 15. Viewer address was baked in at build time
`NEXT_PUBLIC_VIEWER_URL` is inlined into the bundle, tying one portal image to
one environment and forcing a Docker build argument.

Fixed: renamed to `CLOUDFILM_VIEWER_URL` and read at runtime. The same image
digest validated in staging is the one that runs in production; changing a
public hostname is a restart, not a rebuild.

### 16. The development port map was inconsistent
The API answered on 5080 under `dotnet run` but 8080 under Compose, so no single
default could be correct.

Fixed: API 8080, viewer 8081, retrieve 8082 everywhere.

### 17. `outputFileTracingRoot` was broken on Windows
`new URL(...).pathname` yields `/C:/…`. Fixed with `fileURLToPath`.

### 18. A temporal dead zone in the fixture loader
`SEEDED_RECORDS` ran its parser at module load, before the `GENDERS` constant
below it initialised. It fires only when records are non-empty, so typecheck,
lint and the empty-fixture build all passed — it would have thrown a 500 on
every page the first time real data was seeded. Found by running the seed script
against a stubbed Orthanc. Fixed by ordering.

### 19. `process.exit` in a file compiled for the edge runtime
`instrumentation.ts` warned on every request in development. Fixed with the
documented dynamic-import split — and the first attempt at that fix broke
production fail-fast, because a top-level import threw before the `try`. Caught
by re-running the production tests.

---

## Not verified — where failures are still likely

Ordered by dependency. Nothing below has been observed running.

| # | Assumption | If wrong |
| --- | --- | --- |
| 1 | `orthancteam/orthanc:25.4.2` exists in the stable repository | Pull fails; correct `ORTHANC_IMAGE` in `.env` |
| 2 | `python3 /probes/test-aliveness.py` is present and exits 0 | Orthanc never healthy; `retrieve` and `ohif` stall behind it |
| 3 | `bash` is present in `aspnet:9.0-noble` | `api`/`retrieve` never healthy; `portal` stalls |
| 4 | `MigrateAsync()` with **zero migrations** against schema `cloudfilm` does not throw | API exits at start-up. Watch for `schema "cloudfilm" does not exist` |
| 5 | `ohif/app:v3.6.1` exists and serves from `/usr/share/nginx/html` | Blank page, or nginx exits on a bad config mount |
| 6 | The image reads a mounted `app-config.js` (OHIF issue #3835 reports cases where it does not) | Viewer loads but finds no data source |

`pnpm verify:imaging` and `pnpm stack:up` exist to settle 2, 5 and 6 quickly.

---

## Deliberately not changed

- **No EF Core migration was generated.** There are none, and creating one
  requires the .NET SDK, which is unavailable here. Writing an ASP.NET Identity
  migration by hand would be a fabrication. First task of the next sprint:
  `dotnet ef migrations add InitialIdentity`.
- **`ohif` has no health check.** Nothing depends on it, and the portal already
  determines viewer availability from the browser — the only vantage point that
  matters.
- **Image tags, not digests.** Digest pinning is correct for a hospital
  deployment and is documented in `docs/deployment.md`, but a digest cannot be
  obtained without registry access.

---

## Documentation

- `docs/healthchecks.md` — why `curl` is forbidden in minimal images, how each
  probe works, and the order to choose one in.
- `docs/local-imaging.md` — five evidence-gated steps to a real study on screen.
- `docs/architecture.md`, `docs/adr/` — unchanged; no architectural decision was
  reversed by this review.
