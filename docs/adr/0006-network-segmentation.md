# 6. The imaging tier publishes no ports

**Status:** Accepted · Sprint 0

## Context

"Never expose the PACS" is the product's core security promise. It has to be a
property of the deployment topology, not a rule people remember.

The complication: OHIF runs **in the browser** and must fetch DICOMweb over HTTP. Naively
that requires Orthanc to be browser-reachable.

## Decision

Two networks:

| Network | Services | Published ports |
| --- | --- | --- |
| `edge` | `portal`, `api`, `ohif` | Yes |
| `internal` | `api`, `retrieve`, `orthanc`, `postgres`, `redis` | **None** |

Orthanc, PostgreSQL and Redis are on `internal` only. The browser reaches DICOMweb
through the **OHIF service's own nginx**, which proxies `/dicom-web` to Orthanc on the
internal network and attaches Orthanc credentials server-side.

Docker's `internal: true` is deliberately not set on the internal network: the
Retrieve Service needs egress to reach a hospital archive. Isolation comes from not
publishing ports, not from cutting off outbound traffic.

## Consequences

**Good.** The imaging tier is unreachable from the host and from the internet by
construction. Orthanc credentials never reach a browser. There is exactly one code
path from browser to DICOM data, and it is twelve lines of nginx that can be reviewed
in full.

**Costs.** The OHIF container is on both networks, which makes its nginx a security
boundary — it must be reviewed as one. Developers cannot open the Orthanc UI directly;
`docker-compose.dev.yml` exists for that and carries a warning never to apply it to a
hospital deployment.

**Verified by.** `docker compose config` in CI, and by the absence of a `ports:` key on
`orthanc`, `postgres` and `redis` in `docker-compose.yml`.
