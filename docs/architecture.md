# Architecture

## 1. What CloudFilm is

CloudFilm is an orchestration layer. It owns:

- who may see which study, and for how long;
- how a study travels from a hospital archive to a browser;
- the audit trail of both.

It does **not** own DICOM storage, DICOM parsing, image rendering, or the viewer UI.
Those are solved problems with mature implementations, and reimplementing them would
be the single most expensive mistake this project could make.

| Concern | Component | CloudFilm's involvement |
| --- | --- | --- |
| DICOM storage and DICOMweb | Orthanc | Configuration only |
| Viewing, 2D/3D, measurement | OHIF | Configuration and, later, extensions |
| Rendering | Cornerstone3D (inside OHIF) | None |
| Identity storage | ASP.NET Identity | Configuration and policy |
| Everything else | CloudFilm | All of it |

## 2. Layering

```
CloudFilm.Api             composition root, HTTP transport, endpoints
      │  depends on
CloudFilm.Infrastructure  EF Core, Redis, Identity, outbound HTTP
      │  depends on
CloudFilm.Application     use cases and ports (interfaces)
      │  depends on
CloudFilm.Domain          entities, enterprise rules — no package references
```

Dependencies point inwards only. The practical test: `CloudFilm.Domain.csproj` and
`CloudFilm.Application.csproj` contain no `PackageReference` to any infrastructure
library, and adding one should fail review.

**Modular monolith, not microservices.** Feature modules are vertical slices — a
folder in `CloudFilm.Application`, a folder of endpoints in `CloudFilm.Api`, entity
configurations in `CloudFilm.Infrastructure` — deployed as one process. A hospital
deployment is a handful of containers on one host; distributing this system across a
service mesh would add operational cost with no matching benefit.

The one exception is the Retrieve Service, for reasons that are about security rather
than scale (§4).

## 3. Request flow

```
Browser ──► Portal (Next.js, server component)
                │  server-side fetch over the internal network
                ▼
            CloudFilm API ──► PostgreSQL   (accounts, grants, audit)
                │           ──► Redis        (short-lived metadata, TTL enforced)
                │
                └──► Retrieve Service ──► Hospital Adapter ──► Hospital PACS
                                                │
                                                ▼
                                             Orthanc  (staging cache)

Browser ──► OHIF (own origin, iframe) ──► nginx /dicom-web ──► Orthanc
```

The browser talks to exactly two origins: the portal and the viewer. It never learns
an address inside the imaging tier.

## 4. Why the Retrieve Service is a separate process

It would be simpler to make retrieval a module of the API. It is separate because:

- **Blast radius.** It is the only component that opens connections towards a
  hospital network. Keeping it in its own process and its own container lets a
  hospital firewall it, run it inside their DMZ, and audit it in isolation.
- **Replaceability.** The API depends on the Retrieve Service's HTTP contract, not on
  its assemblies. It can be rewritten in Go or Python, or replaced by a hospital's own
  gateway, without recompiling CloudFilm.
- **Failure isolation.** A hospital's archive being slow or down degrades retrieval;
  it must not stall the portal, login, or the audit trail.

There is deliberately **no shared .NET project** between the API and the Retrieve
Service. A shared assembly would quietly re-couple them.

## 5. Hospital Adapter Pattern

Every hospital exposes imaging differently: DICOMweb, DIMSE C-MOVE, a vendor HTTP
API, a file share. `IHospitalAdapter` hides that behind one contract:

```csharp
public interface IHospitalAdapter
{
    string HospitalCode { get; }
    string DisplayName { get; }
    Task<HospitalConnectivity> ProbeAsync(CancellationToken cancellationToken = default);
}
```

Adapters are registered with the container and resolved by hospital code through
`IHospitalAdapterRegistry`. Adding a hospital means **adding an assembly, not editing
existing code** — the Open/Closed Principle applied to the thing that will vary most
across deployments. Duplicate hospital codes are rejected at construction time rather
than producing a non-deterministic route at request time.

No adapters ship by default. A deployment registers what it needs:

```csharp
builder.Services.AddHospitalAdapters();
builder.Services.AddHospitalAdapter<StMaryDicomWebAdapter>();
```

The contract will grow as retrieval is implemented (study query, study fetch); it will
not grow hospital-specific members.

## 6. Viewer integration

OHIF runs as its own container, on its own origin, embedded in an iframe. The portal
builds the URL through `buildViewerUrl()` in `@cloudfilm/core` and knows nothing else
about the viewer.

Why not import OHIF as a React dependency:

- **Upgradeability.** The viewer moves fast. An iframe boundary makes an upgrade an
  image-tag change; a dependency makes it a migration.
- **Isolation.** Viewer JavaScript never shares an execution context with the portal
  session.
- **Start-up budget.** The viewer bundle is large. Loading it only when a study is
  opened is what keeps portal start-up under two seconds.

Customisation goes into OHIF **extensions and modes**, configured in
`services/ohif/app-config.js`. The OHIF source is never forked.

## 7. Caching and data lifetime

CloudFilm holds imaging data transiently; the hospital archive is the system of record.

- Orthanc runs with `MaximumStorageMode: Recycle` and a bounded size, so staged
  studies are evicted automatically.
- `ICacheStore` has no API for writing a value without a TTL. That is a deliberate
  design constraint, not an oversight: `SetAsync` requires a positive `timeToLive` and
  throws otherwise.
- Redis runs with `--maxmemory-policy volatile-ttl` and no persistence. Cached
  imaging metadata never survives a restart.

## 8. Configuration and secrets

All configuration is bound to strongly typed options classes, validated with data
annotations and `ValidateOnStart()`. A misconfigured deployment fails loudly at
start-up instead of at the first patient request.

Secrets exist only in the environment. `.env` is git-ignored; `.env.example` contains
placeholders; `scripts/generate-secrets.sh` produces real ones and refuses to
overwrite an existing file.

## 9. Extension points already in place

| Future work | Plugs in via | Existing code changed |
| --- | --- | --- |
| A new hospital | `IHospitalAdapter` + `AddHospitalAdapter<T>()` | None |
| AI analysis service | A new port in `CloudFilm.Application`, adapter in `CloudFilm.Infrastructure` | Composition root only |
| Object storage (MinIO) | Orthanc storage plugin, or an `ICacheStore` sibling | None |
| A different viewer | `buildViewerUrl()` in `@cloudfilm/core` | One function |
| A different database | `CloudFilm.Infrastructure` | Nothing above it |
