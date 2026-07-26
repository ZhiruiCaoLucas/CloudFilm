# Architecture Decision Records

Each ADR records one decision, the forces behind it, and what it costs. They are
append-only: a decision that is reversed gets a new ADR that supersedes the old one,
rather than an edit that erases the history.

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-orchestrate-mature-open-source.md) | Orchestrate mature open source instead of building imaging infrastructure | Accepted |
| [0002](0002-modular-monolith.md) | Modular monolith, with one deliberate exception | Accepted |
| [0003](0003-retrieve-service-separate-process.md) | The Retrieve Service is a separate process | Accepted |
| [0004](0004-hospital-adapter-pattern.md) | Hospital integrations use the Adapter Pattern | Accepted |
| [0005](0005-viewer-embedded-by-url.md) | OHIF is embedded by URL, never forked | Accepted |
| [0006](0006-network-segmentation.md) | The imaging tier publishes no ports | Accepted |
