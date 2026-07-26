# 5. OHIF is embedded by URL, never forked

**Status:** Accepted · Sprint 0

## Context

The portal must open studies in a viewer. OHIF is a React application, so importing it
as a dependency is technically possible — and tempting, because it would allow tight
UI integration.

OHIF also moves quickly, carries a large bundle, and is the component most likely to
need upgrading for a rendering fix.

## Decision

OHIF runs as its own container on its own origin and is embedded in an iframe. The
portal constructs the URL through `buildViewerUrl()` in `@cloudfilm/core` and knows
nothing else about it.

Customisation goes into OHIF **extensions and modes**, configured in
`services/ohif/app-config.js`. The OHIF source is never forked or patched.

## Consequences

**Good.**

- *Upgrades are an image-tag change*, not a dependency migration.
- *Isolation.* Viewer JavaScript never shares an execution context with the portal
  session.
- *Start-up budget.* The viewer bundle loads only when a study is opened, which is
  what keeps portal start-up under two seconds.
- *Replaceability.* Swapping the viewer is a change to one function.

**Costs.** Portal-to-viewer communication is limited to URL parameters and, if needed
later, `postMessage`. Deep UI integration — portal chrome around viewer state — is not
available. That is an acceptable price; if it ever stops being acceptable, an OHIF
mode is the answer, not a fork.

**Constraint.** The viewer needs cross-origin isolation headers for SharedArrayBuffer
volume rendering, which is why `services/ohif/default.conf` sets COOP and COEP.
