# 4. Hospital integrations use the Adapter Pattern

**Status:** Accepted · Sprint 0

## Context

CloudFilm will run in multiple hospitals. Each exposes imaging differently: DICOMweb,
DIMSE C-MOVE, a vendor HTTP API, sometimes a file share. Authentication, identifiers
and quirks differ per site.

This is the dimension along which the system will vary most, and it will keep varying
after CloudFilm is open source and deployed by people we will never meet.

## Decision

Every hospital integration implements one port:

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
existing code**.

No adapters ship by default. `HospitalAdapterRegistry` rejects duplicate hospital
codes at construction time.

## Consequences

**Good.** Open/Closed applied where variation actually lives. The retrieve pipeline
contains no hospital-specific branching. A site-specific integration can live in a
private repository without forking CloudFilm.

**Costs.** The contract must stay hospital-agnostic. Pressure to add a member that
serves exactly one site is the failure mode to watch for; such a need belongs in that
adapter's own configuration, not in the port.

**Deliberately empty.** Shipping a default or mock adapter would put a fake
implementation on the production path. A deployment with no adapters reports
**Degraded**, which is an honest description of its state.
