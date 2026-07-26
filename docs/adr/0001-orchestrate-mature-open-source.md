# 1. Orchestrate mature open source instead of building imaging infrastructure

**Status:** Accepted · Sprint 0

## Context

CloudFilm needs DICOM storage, DICOMweb, a viewer, 2D and 3D rendering, and support
for CT, CBCT and panoramic X-ray. All of these exist as mature, clinically deployed
open-source projects: Orthanc, OHIF, Cornerstone3D.

Building any of them is a multi-year effort with a regulatory tail. Building them
badly is worse than not building them.

## Decision

CloudFilm integrates upstream components and writes only the logic that is genuinely
CloudFilm's: who may see which study, how it gets there, and the audit trail.

Upstream components are consumed as **published container images with configuration
mounted in**. They are not vendored, patched or forked.

## Consequences

**Good.** The hard imaging problems are solved by projects with more clinical
deployment hours than CloudFilm will have for years. Upgrades are image-tag changes.
The codebase stays small enough to review.

**Costs.** CloudFilm inherits upstream's release cadence and defects, and is
constrained to what upstream's configuration surface allows. When configuration is not
enough, the answer is an upstream contribution or an extension — not a fork.

**Enforced by.** `services/*` contains configuration only. Any pull request adding
vendored upstream source is rejected.
