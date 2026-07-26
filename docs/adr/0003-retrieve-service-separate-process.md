# 3. The Retrieve Service is a separate process

**Status:** Accepted · Sprint 0

## Context

Something must reach into a hospital's PACS or VNA and pull studies out. That
component is the single most security-sensitive part of CloudFilm: it holds hospital
credentials and it is the only thing with a route into a clinical network.

ADR 0002 argues for a monolith. This is the exception.

## Decision

Retrieval runs as its own process, its own container and its own image. The API
communicates with it over HTTP. There is **no shared .NET project** between them.

## Consequences

**Good.**

- *Blast radius.* Hospital credentials and hospital network access live in one small
  container that can be firewalled, deployed into a DMZ, and audited on its own.
- *Replaceability.* The API depends on an HTTP contract, not on assemblies. The
  Retrieve Service can be rewritten in another language, or replaced by a hospital's
  own gateway, with no change to CloudFilm.
- *Failure isolation.* A slow or unreachable hospital archive degrades retrieval only.
  Login, the portal and the audit trail keep working.

**Costs.** One more image to build and monitor. Calls between the API and retrieval
are network calls, with the failure modes that implies — which is why the API's
readiness probe includes the Retrieve Service.

**Rejected alternative.** A shared `CloudFilm.Contracts` assembly would remove some
duplication but would quietly recouple the two, defeating the replaceability that
motivates the split.
