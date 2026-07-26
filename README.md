# CloudFilm

Cloud imaging platform that gives patients and clinicians browser access to hospital
imaging studies **without exposing the hospital PACS network**.

CloudFilm is not a PACS and not a DICOM viewer. It orchestrates mature open-source
components — [Orthanc](https://www.orthanc-server.com/) for DICOM,
[OHIF](https://ohif.org/) for viewing, [Cornerstone3D](https://www.cornerstonejs.org/)
for rendering — and supplies only the business logic that sits between them:
authentication, hospital integration, access control and audit.

> **Status:** Sprint 0 — repository initialised. Every service builds, the stack starts,
> and no feature logic has been written yet. See [Roadmap](#roadmap).

---

## Architecture

```
Patient / Clinician
        │
        ▼
   CloudFilm Portal ──────────────► OHIF Viewer  (iframe, own origin)
   (Next.js)                              │
        │ server-side, internal only      │ /dicom-web (nginx proxy)
        ▼                                 ▼
   CloudFilm API ───────────────────►  Orthanc
   (ASP.NET Core 9)                   (DICOM staging cache)
        │      │                          ▲
        │      │                          │ stages studies
        │      └──────────────► Retrieve Service
        │                       (Hospital Adapter Pattern)
        │                                 │
   PostgreSQL + Redis                     ▼
                                  Hospital PACS / VNA
```

Three boundaries carry most of the design:

1. **The Retrieve Service is a separate process, not a module.** It is the only
   component allowed to open a connection towards a hospital network, so it can be
   firewalled and deployed separately — and replaced entirely, in any language,
   because the API depends on its HTTP contract rather than its code.
2. **The viewer is embedded by URL, not imported.** OHIF is never forked. It is
   upgraded by changing an image tag.
3. **The DICOM tier has no published port.** Browsers reach imaging data only
   through the OHIF service's reverse proxy.

Full detail: [`docs/architecture.md`](docs/architecture.md).
Decision records: [`docs/adr/`](docs/adr/).

---

## Quick start

Requirements: Docker with Compose v2, and — for working on the code — .NET SDK 9 and
Node 22 with pnpm 11.

```bash
git clone <repository-url> cloudfilm
cd cloudfilm

./scripts/generate-secrets.sh   # writes .env with generated secrets
docker compose up --build -d
docker compose ps               # wait for every service to report healthy
```

| Surface | URL |
| --- | --- |
| Portal | <http://localhost:3000> |
| API (OpenAPI UI in Development) | <http://localhost:8080/scalar/v1> |
| API readiness | <http://localhost:8080/health/ready> |
| OHIF Viewer | <http://localhost:8081> |

Orthanc, PostgreSQL and Redis are intentionally unreachable from the host. To expose
them while developing:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

`make help` lists every shortcut.

---

## Working on the code

```bash
# Backend
dotnet build CloudFilm.sln --configuration Release   # warnings are errors
dotnet test  CloudFilm.sln --configuration Release

# Frontend
pnpm install
pnpm lint && pnpm typecheck && pnpm build

# Everything CI runs
make verify
```

### Running services outside Docker

```bash
docker compose up -d postgres redis orthanc      # dependencies only
dotnet run --project apps/api/src/CloudFilm.Api           # http://localhost:5080
dotnet run --project apps/retrieve/src/CloudFilm.Retrieve # http://localhost:5081
pnpm dev                                                  # http://localhost:3000
```

### Database migrations

No migration exists yet — creating the initial ASP.NET Identity migration is the first
task of Sprint 1:

```bash
dotnet ef migrations add InitialIdentity \
  --project apps/api/src/CloudFilm.Infrastructure \
  --startup-project apps/api/src/CloudFilm.Api
```

`Infrastructure__AutoMigrate` applies migrations at start-up. It is `true` for the
Compose stack so the database provisions itself, and must be `false` in a hospital
deployment, where schema changes belong to a controlled release.

---

## Repository layout

```
apps/
  portal/            Next.js portal (App Router, TypeScript, Tailwind)
  api/               ASP.NET Core 9 modular monolith
    src/CloudFilm.Domain/           entities and enterprise rules, zero dependencies
    src/CloudFilm.Application/      use cases and ports
    src/CloudFilm.Infrastructure/   EF Core, Redis, Identity — the adapters
    src/CloudFilm.Api/              composition root, transport, endpoints
    tests/
  retrieve/          Retrieve Service (separate process)
    src/CloudFilm.Retrieve.Hospital/  IHospitalAdapter — the integration port
    src/CloudFilm.Retrieve/           host
    tests/
packages/
  shared/            transport contracts mirroring the .NET DTOs
  core/              framework-agnostic client logic (API client, viewer URLs)
  ui/                design system: accessible, dark-mode-aware React primitives
services/
  orthanc/           Orthanc configuration (upstream image, unmodified)
  ohif/              OHIF configuration and DICOMweb reverse proxy
docker/              Dockerfiles
docs/                architecture, deployment, decision records
scripts/             setup and operational helpers
```

Dependencies point inwards only: `Api → Infrastructure → Application → Domain`.
The Domain project has no package references at all, and that is enforced by review.

---

## Security posture

| Control | How |
| --- | --- |
| PACS never exposed | Only the Retrieve Service may reach a hospital network. |
| DICOM tier unreachable | Orthanc, PostgreSQL and Redis publish no ports; browsers use the OHIF proxy. |
| Transient storage | Orthanc recycles studies; every Redis entry carries a mandatory TTL. |
| Authentication | ASP.NET Identity with JWT bearer tokens; 12-character minimum passwords, lockout, confirmed e-mail. |
| Authorisation | Role-based, added per feature module. |
| Audit | Every request logged with subject, correlation id and client address. |
| Error hygiene | RFC 9457 problem details; internal detail never leaves the process. |
| Secrets | Environment only. `.env` is git-ignored; `.env.example` holds no real values. |

CloudFilm is a transient cache in front of the hospital archive. **The hospital archive
remains the system of record.**

---

## Roadmap

| Sprint | Outcome |
| --- | --- |
| **0 — done** | Repository, build, Docker stack, CI. Everything compiles and runs. |
| 1 | Identity schema, registration and login, JWT issuance, role model. |
| 2 | First hospital adapter (DICOMweb), study staging into Orthanc, retrieve API. |
| 3 | Study list and viewer launch in the portal, access-grant model. |
| 4 | Sharing links with expiry, download, full audit trail. |
| 5 | AI analysis service (Python FastAPI) plugged in behind an existing port. |

One sprint at a time; every sprint leaves the application runnable.

---

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md). In short: Conventional Commits, small
commits, and a change is done only when the solution builds warning-free, tests pass,
`docker compose up` reaches healthy, and the docs match the code.

## Licence

Apache-2.0 — see [`LICENSE`](LICENSE).
