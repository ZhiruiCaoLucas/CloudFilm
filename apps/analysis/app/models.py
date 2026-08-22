"""The shape of what this service returns.

Mirrors the CloudFilm Application contract (`StudyAnalysis`, `AnalysisFinding`)
across the HTTP boundary, in the same way the TypeScript packages mirror the .NET
DTOs. Field names are serialised in camelCase because that is what the .NET
client reads and writes; the Python side keeps snake_case internally.

Nothing here carries patient identity. That is not an oversight to be corrected
when somebody needs a name - it is the boundary this service is on the wrong side
of, and a field added here would travel to every client that renders a finding.
"""

from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


def _camel(name: str) -> str:
    """Convert a snake_case field name to camelCase."""
    head, *tail = name.split("_")

    return head + "".join(word.capitalize() for word in tail)


class _Contract(BaseModel):
    """Base for every type that crosses the wire."""

    model_config = ConfigDict(alias_generator=_camel, populate_by_name=True, extra="forbid")


class AnalysisFinding(_Contract):
    """One thing the characterisation observed.

    The label carries what was observed and, where it is a measurement, its value
    - ``rows=512``, ``modality=CT``. That reads oddly for a numeric fact, and it
    is a deliberate consequence of the Application contract having exactly two
    fields: a label and a score in [0, 1]. Structural facts do not fit a
    probability, so they are stated in the label and the score reports certainty.

    If a later sprint gives `AnalysisFinding` a typed value, this is the first
    thing that should change. Until then, one convention in one place is better
    than a score that means "512".
    """

    label: str = Field(max_length=128)
    score: float = Field(ge=0.0, le=1.0)


class StudyAnalysis(_Contract):
    """What this service says about one study."""

    study_instance_uid: str = Field(max_length=128)
    model_name: str = Field(max_length=128)
    model_version: str = Field(max_length=64)

    #: Named to match the Application contract's `CompletedAtUtc`, not the
    #: sprint brief's `AnalysedAtUtc`. The .NET record is already GREEN and is
    #: what the S8.3 adapter will deserialise into; the two names must not drift.
    completed_at_utc: datetime

    findings: list[AnalysisFinding]


class ServiceError(_Contract):
    """The only body a failed request receives.

    One field, and a message chosen from a fixed set. A parser's own words about
    a DICOM file routinely quote the tag it choked on, and a tag value can be a
    patient's name - so nothing the parser says is ever passed on.
    """

    detail: str


class HealthStatus(_Contract):
    """The liveness answer.

    A constant. A health endpoint that reports versions, paths or package lists
    is an unauthenticated inventory of the host, and this one is reachable by
    anything on the internal network.
    """

    status: str
