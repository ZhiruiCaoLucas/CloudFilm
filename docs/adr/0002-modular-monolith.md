# 2. Modular monolith, with one deliberate exception

**Status:** Accepted · Sprint 0

## Context

CloudFilm will be deployed in individual hospitals — typically a handful of containers
on one host, operated by staff whose primary job is not running distributed systems.
The team building it is small.

Microservices would buy independent scaling and deployment. Neither is a constraint
here; operational simplicity is.

## Decision

The API is a **modular monolith**. Feature modules are vertical slices — a folder in
`CloudFilm.Application`, endpoints in `CloudFilm.Api`, entity configurations in
`CloudFilm.Infrastructure` — deployed as a single process.

Layer dependencies point inwards only:

```
Api → Infrastructure → Application → Domain
```

`CloudFilm.Domain` has no package references at all.

One component is deliberately excluded: the Retrieve Service (ADR 0003).

## Consequences

**Good.** One process to deploy, one log stream, one transaction scope. Refactoring
across modules is a compiler-checked operation rather than a versioned API change.

**Costs.** Modules can only be enforced by discipline and review; nothing at runtime
prevents a module from reaching into another. Scaling is all-or-nothing.

**If this needs to change**, the layering means a module can be extracted into its own
process by replacing its port implementation with an HTTP adapter — the same seam
already used for the Retrieve Service.
