# Deployment

## Topology

One Docker Compose stack per hospital. Seven services, two networks:

| Network | Services | Published |
| --- | --- | --- |
| `edge` | `portal`, `api`, `ohif` | Yes — behind the hospital's ingress/TLS terminator |
| `internal` | `api`, `retrieve`, `orthanc`, `postgres`, `redis` | **No ports, ever** |

`api` and `ohif` sit on both networks; that is the only path from a browser to
anything imaging-related.

## Prerequisites

- Docker Engine 24+ with Compose v2
- A TLS terminator in front of the stack (nginx, Traefik, or the hospital's existing
  ingress). CloudFilm serves plain HTTP inside the compose network and expects TLS to
  be terminated at the edge; `UseHsts()` is enabled outside Development as defence in
  depth.
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

## Pin the third-party images

`ORTHANC_IMAGE` and `OHIF_IMAGE` default to `:latest` so a fresh clone runs. Before a
hospital deployment, pin both to an immutable digest:

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

## Health and monitoring

| Probe | Meaning | Use for |
| --- | --- | --- |
| `GET /health/live` | The process is up | Orchestrator restart policy |
| `GET /health/ready` | PostgreSQL, Redis, Retrieve Service and Orthanc all reachable | Load-balancer membership |

The Retrieve Service's readiness additionally probes every registered hospital
adapter. A deployment with no adapters configured reports **Degraded**, not Unhealthy —
that distinguishes "not configured yet" from "configured but broken".

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

- Never apply `docker-compose.dev.yml` to a hospital deployment. It publishes
  PostgreSQL, Redis and Orthanc to the host.
- Never give the `retrieve` container broader network access than the specific
  hospital endpoints it integrates with.
- Rotate `JWT_SIGNING_KEY` on a schedule; rotation invalidates outstanding tokens,
  which is the intended behaviour.
