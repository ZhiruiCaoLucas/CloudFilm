# Folder structure

```
CloudFilm/
├── apps/
│   ├── portal/                          Next.js portal
│   │   ├── src/app/                     App Router routes
│   │   │   ├── page.tsx                 landing page, platform status
│   │   │   ├── viewer/[studyInstanceUid]/  OHIF host page
│   │   │   └── api/health/route.ts      container liveness probe
│   │   ├── src/components/              app-specific components
│   │   ├── src/lib/                     env validation, API client factory
│   │   └── next.config.ts               standalone output, transpiled workspace packages
│   │
│   ├── api/                             ASP.NET Core 9 modular monolith
│   │   ├── src/CloudFilm.Domain/        entities, enterprise rules — no dependencies
│   │   ├── src/CloudFilm.Application/   use cases and ports
│   │   ├── src/CloudFilm.Infrastructure/  EF Core, Redis, Identity
│   │   ├── src/CloudFilm.Api/           composition root, endpoints
│   │   └── tests/CloudFilm.Api.Tests/
│   │
│   └── retrieve/                        Retrieve Service — separate process
│       ├── src/CloudFilm.Retrieve.Hospital/  IHospitalAdapter, registry
│       ├── src/CloudFilm.Retrieve/           host, health, endpoints
│       └── tests/CloudFilm.Retrieve.Tests/
│
├── packages/                            TypeScript workspace packages (source, not built)
│   ├── shared/                          transport contracts mirroring the .NET DTOs
│   ├── core/                            API client, viewer URL construction
│   └── ui/                              design system primitives
│
├── services/                            configuration for upstream components
│   ├── orthanc/orthanc.json             structure only; secrets via ORTHANC__* env vars
│   └── ohif/                            app-config.js + nginx DICOMweb proxy
│
├── docker/                              api / retrieve / portal Dockerfiles
├── docs/                                architecture, deployment, ADRs
├── scripts/                             secret generation, database bootstrap
├── .github/workflows/                   CI, CodeQL
│
├── CloudFilm.sln                        all eight .NET projects
├── Directory.Build.props                repo-wide MSBuild policy (warnings as errors)
├── Directory.Packages.props             central NuGet versions
├── docker-compose.yml                   production-shaped stack
├── docker-compose.dev.yml               development overlay (publishes the data tier)
├── eslint.config.mjs                    one flat config for the whole workspace
├── tsconfig.base.json                   strict TypeScript baseline
└── Makefile                             developer shortcuts
```

## Conventions

**Where does new code go?**

| You are adding | Put it in |
| --- | --- |
| A domain entity or invariant | `CloudFilm.Domain` |
| A use case, or a port for something external | `CloudFilm.Application` |
| An implementation of a port (database, cache, HTTP) | `CloudFilm.Infrastructure` |
| An HTTP endpoint | `CloudFilm.Api/Endpoints/<Module>` |
| A hospital integration | A new project implementing `IHospitalAdapter` |
| A type crossing the .NET/TypeScript boundary | `packages/shared` |
| Client logic with no React dependency | `packages/core` |
| A reusable visual component | `packages/ui` |
| A page or app-specific component | `apps/portal/src` |

**Why workspace packages ship source.** `packages/*` expose `src/index.ts` directly
and are compiled by Next through `transpilePackages`. There is no build ordering to
maintain and no stale `dist/` to debug. The trade-off — they are only consumable by a
bundler — is acceptable because the portal is the only consumer.

**Test placement.** Tests live beside the app they cover, under `apps/*/tests/`. The
suites are free of Docker and database dependencies so they run in seconds on every
commit; integration tests requiring live PostgreSQL, Redis and Orthanc will land as a
separate suite with the first feature module.
