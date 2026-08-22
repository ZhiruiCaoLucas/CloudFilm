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

Two components sit outside that process, both for reasons about security rather than
scale: the Retrieve Service (§4) and the analysis service (§9).

## 3. Request flow

```
Browser ──► Portal (Next.js, server component)
                │  server-side fetch over the internal network
                ▼
            CloudFilm API ──► PostgreSQL   (accounts, grants, audit)
                │           ──► Redis        (short-lived metadata, TTL enforced)
                │
                ├──► Retrieve Service ──► Hospital Adapter ──► Hospital PACS
                │                               │
                │                               ▼
                │                            Orthanc  (staging cache)
                │
                └──► Analysis Service  (Python, internal only)
                     the API fetches the study and posts the bytes;
                     the service is never told where imaging lives

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

The viewer reaches DICOM over its own path, which never passes through the API, so
that path is authorised at its own edge:

```
Portal  ──►  POST .../viewer-session        (or /share/{token} for a recipient)
             ├─ authorises the caller: study access policy, or the share link
             └─ returns an opaque session id, bound to ONE StudyInstanceUID

Browser ──►  Set-Cookie: cloudfilm_viewer   (HttpOnly, host-only, ~15 minutes)
        ──►  OHIF (:8081)
             └─ nginx  ──auth_request──►  API DICOMweb gate
                                          ├─ cookie resolves to a session?
                                          ├─ does the request name that study?
                                          └─ is the share link still live?
                        204 ──►  Orthanc (internal network only)
                        401 ──►  nginx rewrites to an empty 404
```

The portal never links the iframe straight at the viewer. It points at a portal
route that obtains a session for the study on screen, sets the cookie and redirects
— because a page cannot set a cookie while rendering, and the viewer's first
DICOMweb request must already carry one.

Two properties this buys, both enforced in one place: a viewer can reach exactly the
study it was admitted to, and an unfiltered study-list query — the archive's whole
index — is refused. OHIF's own study list is switched off in `app-config.js`
accordingly.

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
- Redis also holds viewer-session state — the short-lived record of which study a
  browser has been admitted to — and the share-link rate-limit counters. Both are
  deliberately transient: a session cannot outlive its own time to live even if
  nothing deletes it. The consequence is that a restart, or eviction under memory
  pressure, ends viewing in progress. That fails closed, and the viewer's next
  request is refused rather than admitted.

## 8. Configuration and secrets

All configuration is bound to strongly typed options classes, validated with data
annotations and `ValidateOnStart()`. A misconfigured deployment fails loudly at
start-up instead of at the first patient request.

Secrets exist only in the environment. `.env` is git-ignored; `.env.example` contains
placeholders; `scripts/generate-secrets.sh` produces real ones and refuses to
overwrite an existing file.

## 9. Automated analysis

A study can be sent for automated characterisation: instance count, modality,
geometry, bit depth, and a normalised mean pixel intensity. **Nothing it produces is
a clinical finding**, and the portal says so wherever a result appears.

It runs as its own process, in Python, for the same reason the Retrieve Service does
- what it is allowed to reach, not how fast it is:

```
POST /api/v1/imaging/studies/{uid}/analysis
        │
        ├─ RequiresApplicationRole            a role, or 403
        ├─ IStudyAccessPolicy (Download)      denied ⇒ 404, before anything is fetched
        ├─ IStudyDownloadSource               the study, streamed from Retrieve
        ├─ IStudyAnalyser ──► analysis        the bytes, over the internal network
        └─ ANALYSE_STUDY                      recorded before the result is returned
```

Three properties hold this together:

1. **Authorisation happens before retrieval.** A caller who may not see the study
   never causes the archive to produce it.
2. **The analyser is handed bytes, not an address.** `IStudyAnalyser` takes an open
   `StudyDownload`; there is no identifier for it to resolve and no URL field
   anywhere in the request. It makes no outbound call of any kind, so it cannot
   reach imaging nobody authorised - a property of the port's shape rather than of
   the service's good behaviour.
3. **It publishes no port.** It authenticates nobody, so its only protection is that
   nothing outside the compose network can address it. `SECURITY.md` covers the
   consequences, including that it holds PHI: DICOM carries patient identifiers in
   its own tags whatever the request envelope says.

The capability is optional. Without `Downstream:AnalysisServiceUrl` the API registers
no analyser, the endpoint answers `501`, and the portal reports that analysis is
unavailable in this deployment.

The adapter bridges two multiparts that are not the same shape: the archive answers
WADO-RS with `multipart/related`, and the service accepts a `multipart/form-data`
upload. `DicomFormDataContent` rewrites one into the other while the request is being
written, one instance at a time, so a study is never held in memory.

## 10. Extension points already in place

| Future work | Plugs in via | Existing code changed |
| --- | --- | --- |
| A new hospital | `IHospitalAdapter` + `AddHospitalAdapter<T>()` | None |
| A different analyser | `IStudyAnalyser` — the port §9 already produced | Composition root only |
| Object storage (MinIO) | Orthanc storage plugin, or an `ICacheStore` sibling | None |
| A different viewer | `buildViewerUrl()` in `@cloudfilm/core` | One function |
| A different database | `CloudFilm.Infrastructure` | Nothing above it |
