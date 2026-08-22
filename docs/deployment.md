# Deployment

## Topology

One Docker Compose stack per hospital. Eight services, two networks:

| Network | Services | Published |
| --- | --- | --- |
| `edge` | `portal`, `api`, `ohif` | Yes — behind the hospital's ingress/TLS terminator |
| `internal` | `api`, `retrieve`, `analysis`, `orthanc`, `postgres`, `redis` | **No ports, ever** |

`api` and `ohif` sit on both networks; that is the only path from a browser to
anything imaging-related.

## Prerequisites

- Docker Engine 24+ with Compose v2
- A TLS terminator in front of the stack (nginx, Traefik, or the hospital's existing
  ingress). CloudFilm serves plain HTTP inside the compose network and expects TLS to
  be terminated at the edge; `UseHsts()` is enabled outside Development as defence in
  depth.
- **That terminator must set `X-Forwarded-Proto`.** The portal decides whether to
  mark its session cookie and the viewer-session cookie `Secure` from that header,
  not from the build mode — because the compose stack runs a production build over
  plain HTTP on localhost, where `Secure` would make the browser discard the cookie
  and sign-in would appear to succeed and then do nothing. A terminator that omits
  the header leaves both cookies without `Secure` over what the browser believes is
  HTTPS.
- **The portal and the viewer must be served from the same hostname.** Different
  ports are fine — cookies ignore ports — but the viewer-session cookie is host-only
  and carries no `Domain`, so a viewer on a different hostname receives no cookie and
  is refused every image. See `SECURITY.md`.
- Outbound network access from the `retrieve` container to the hospital archive, and
  nothing else.

## First deployment

```bash
git clone <repository-url> cloudfilm && cd cloudfilm

./scripts/generate-secrets.sh
$EDITOR .env        # set PORTAL_PUBLIC_URL and VIEWER_PUBLIC_URL to real hostnames

docker compose up --build -d
docker compose ps   # every service must reach healthy
```

`PORTAL_PUBLIC_URL` and `VIEWER_PUBLIC_URL` are the addresses **a browser** uses. They
drive CORS on the API and the viewer iframe URL, so they must be the externally
visible hostnames, not container names.

Both are read at **runtime**, not baked in at build time. The portal image is
therefore environment-agnostic: the same digest that passed validation in staging
is the one that runs in production. Changing a public hostname is a restart, not
a rebuild.

## Pin the third-party images

`ORTHANC_IMAGE` and `OHIF_IMAGE` are version-pinned in `docker-compose.yml`, never
`:latest`. A tag is still mutable - the publisher can move it - so pin both to an
immutable digest before a hospital deployment, and record which digest was verified:

```bash
docker pull orthancteam/orthanc:<version>
docker inspect --format='{{index .RepoDigests 0}}' orthancteam/orthanc:<version>
# ORTHANC_IMAGE=orthancteam/orthanc@sha256:...
```

A clinical deployment whose image can change underneath it is not reproducible, and
cannot be audited after an incident.

## Database schema

`API_AUTO_MIGRATE=true` applies outstanding EF Core migrations on start-up. Keep it on
for local and staging. **Set it to `false` in production** and apply migrations as an
explicit release step:

```bash
dotnet ef database update \
  --project apps/api/src/CloudFilm.Infrastructure \
  --startup-project apps/api/src/CloudFilm.Api \
  --connection "Host=...;Database=cloudfilm;Username=...;Password=..."
```

CloudFilm and Orthanc share a PostgreSQL server but never a database. Orthanc's index
is upstream software's schema and stays outside CloudFilm's migration history;
`scripts/init-databases.sh` provisions it on first start of an empty volume.

## The analysis service

An internal Python service that characterises one study's DICOM: how many instances
arrived, their modality, geometry and bit depth, and a normalised mean pixel
intensity. **It is not a diagnostic model and produces no clinical finding.**

| Property | Value |
| --- | --- |
| Listens on | `8000`, inside its container |
| Published to the host | **Never**, in either compose file |
| Reached by | The API only, at `http://analysis:8000`, over `internal` |
| Configured by | `Downstream__AnalysisServiceUrl`, set in `docker-compose.yml` |
| Authentication | **None** |
| Persistence | None. It keeps nothing between requests |

It authenticates nobody by design. Everything that decides who may have a study
analysed — the caller's role, then `IStudyAccessPolicy` with the `Download`
permission — happens in the API, before a byte is fetched, and the service is handed
the bytes rather than an identifier it could resolve. That is safe exactly as long as
nothing outside the compose network can reach it, which is why it publishes no port
and why publishing one, or fronting it with the ingress, would defeat every control
above it.

The capability is **optional**. Remove `Downstream__AnalysisServiceUrl` and the API
registers no analyser, the endpoint answers `501`, and the portal reports that
analysis is unavailable in this deployment. A CloudFilm without it is complete.

| Variable | Default | Meaning |
| --- | --- | --- |
| `ANALYSIS_MAX_UPLOAD_BYTES` | `67108864` (64 MiB) | Largest upload accepted, counted across the whole request |

A real study can exceed 64 MiB. Raising the limit is deliberate, and should be done
together with the temporary-file caveat in
[`apps/analysis/README.md`](../apps/analysis/README.md): the web framework spools
large uploads to the container's temporary directory, so mount it as `tmpfs` if that
matters to you.

### Deploying and verifying it

```bash
docker compose up -d --build analysis
docker compose ps analysis                 # wait for healthy

# The API must be recreated to pick up the address, not restarted:
# `restart` does not re-read the environment.
docker compose up -d --force-recreate api

docker compose exec api printenv Downstream__AnalysisServiceUrl   # http://analysis:8000
docker compose exec analysis python -c \
  "import urllib.request; print(urllib.request.urlopen('http://127.0.0.1:8000/health').read())"
```

Then open a study in the portal and press **Run analysis**. A successful run also
writes one `ANALYSE_STUDY` audit row, which is the server-side confirmation that the
whole path worked.

## Health and monitoring

| Probe | Meaning | Use for |
| --- | --- | --- |
| `GET /health/live` | The process is up | Orchestrator restart policy |
| `GET /health/ready` | PostgreSQL, Redis and the Retrieve Service reachable — and, through the Retrieve Service's own readiness, Orthanc | Load-balancer membership |

The API reaches Orthanc's health through the Retrieve Service rather than probing
it directly, because the API holds no Orthanc credential and must not be given
one: Retrieve is the only component permitted to reach the imaging tier. Orthanc
answers an anonymous request with `401` whenever `AuthenticationEnabled` is on,
so a direct probe could only ever have been healthy in a stack running the
development overlay.

The Retrieve Service's readiness probes every registered hospital adapter, which
for the Orthanc adapter means a credentialed `dicom-web/studies?limit=1` — so a
healthy answer means the DICOMweb plugin is serving, not merely that a port
accepts. A deployment with no adapters configured reports **Degraded**, not
Unhealthy — that distinguishes "not configured yet" from "configured but broken".

Logs are structured JSON (Serilog `CompactJsonFormatter`) on stdout, with subject,
correlation id and client address on every request. Ship them with the hospital's
existing collector; the correlation id in an error response maps directly to a log entry.

## Backup

| Data | Volume | Backup |
| --- | --- | --- |
| Accounts, grants, audit | `postgres-data` | Required — this is the only irreplaceable state |
| Staged DICOM | `orthanc-storage` | Not required; refetchable from the hospital archive |
| Redis | none (no persistence) | Not applicable |

## Upgrading

```bash
git pull
docker compose build
docker compose up -d          # rolling within the stack; dependencies gate start-up
docker compose logs -f api
```

Roll back by checking out the previous tag and repeating. Because migrations are
applied deliberately in production, a rollback of application code does not silently
strand the schema.

## Operational safety

- Never apply `docker-compose.dev.yml` to a hospital deployment. What it changes:

  | Service | Base file | Dev overlay |
  | --- | --- | --- |
  | PostgreSQL | `127.0.0.1:5432` | `5432` on every interface |
  | Redis | `127.0.0.1:6379` | `6379` on every interface |
  | Retrieve | not published | `127.0.0.1:8082` — loopback only, and unauthenticated |
  | Analysis | not published | **still not published** |
  | Orthanc | not published | **still not published** |
  | Orthanc authentication | on, credential injected at the proxy | **off** (`services/orthanc/zz-dev.json`) |

  Redis now holds viewer-session state, so publishing it is an access-control
  concern and not merely a cache one.
- Orthanc has no host port in either file. Reaching it from the host is a
  deliberate, temporary override — see `docs/local-imaging.md` — and in development
  that override exposes an archive with authentication disabled.
- Never give the `retrieve` container broader network access than the specific
  hospital endpoints it integrates with.
- Rotate `JWT_SIGNING_KEY` on a schedule; rotation invalidates outstanding tokens,
  which is the intended behaviour.
