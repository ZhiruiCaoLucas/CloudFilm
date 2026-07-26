# Contributing to CloudFilm

CloudFilm runs in hospitals. The bar is set accordingly.

## Definition of done

A change is complete only when all of these hold:

- `dotnet build CloudFilm.sln --configuration Release` succeeds with **no warnings**
  (warnings are errors repository-wide)
- `dotnet test CloudFilm.sln` passes
- `pnpm lint && pnpm typecheck && pnpm build` passes
- `docker compose up --build` reaches healthy for every service
- README or `docs/` updated if the architecture changed
- No `TODO`, dead code, commented-out code, or unused symbols introduced

`make verify` runs everything CI runs.

## Workflow

```
Design → Implement → Build → Lint → Test → Commit → Document
```

Discuss architecture **before** implementing it. If a change affects a boundary in
[`docs/architecture.md`](docs/architecture.md), open an issue first. If it changes a
decision, write an ADR.

Work one sprint at a time. Every sprint leaves the application runnable.

## Commits

[Conventional Commits](https://www.conventionalcommits.org/), small and frequent:

```
feat(api): issue JWT access tokens on successful sign-in
fix(retrieve): reject duplicate hospital codes at start-up
refactor(portal): move viewer URL construction into @cloudfilm/core
docs(adr): record why the retrieve service is a separate process
test(api): cover cache expiry on ICacheStore
chore(deps): update Npgsql to 9.0.3
```

Types: `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `build`, `ci`, `chore`.

Every commit must compile. A branch that bisects to a broken build is a bug in the
branch.

## Code standards

**C#**

- SOLID; composition over inheritance
- Constructor injection; no service locator
- Business logic never references EF Core, Redis or `HttpContext`
- Nullable reference types on, strict; no `!` without a comment justifying it
- Public members carry XML documentation — it is enforced by the compiler
- No suppressed warnings

**TypeScript**

- `strict`, plus `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`
- No `any`; prefer `unknown` and narrow
- Business logic in `packages/core`, not in components
- Components stay presentational and accessible

**Both**

- Clarity over cleverness
- Name things after what they mean in the domain
- A comment explains *why*; the code already says *what*

## Tests

Unit tests run without Docker, a database or a network. Anything that needs live
infrastructure belongs in the integration suite.

Test behaviour, not implementation. `SetAsync_RejectsNonPositiveTimeToLive` is a
useful test; `SetAsync_CallsDistributedCache` is not.

## Security

Never commit a secret. Never log patient data, credentials or DICOM identifiers
beyond a Study Instance UID. Never widen the `retrieve` container's network access.
Never publish a port for Orthanc, PostgreSQL or Redis in `docker-compose.yml`.

Report vulnerabilities privately — see [`SECURITY.md`](SECURITY.md).
