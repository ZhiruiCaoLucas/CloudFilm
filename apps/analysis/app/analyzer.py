"""Characterisation of DICOM instances.

This is not a diagnostic model and does not attempt to be one. It reports what
the file says about itself - modality, geometry, bit depth, how many instances
arrived - plus one reproducible pixel statistic. Every value is derived from the
input alone, so the same bytes always produce the same result.

No clinical label is produced anywhere in this module, deliberately. A label like
"pneumonia" would be a claim this system cannot support, and putting one in front
of a clinician because it was easy to compute would be worse than returning
nothing.

Nothing here logs, prints, or writes to disk. The dataset is held for as long as
one instance takes to summarise and then dropped.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from typing import BinaryIO

import pydicom
from pydicom.errors import InvalidDicomError

from .models import AnalysisFinding, StudyAnalysis

#: What produced a result. Stable, and not the name of a clinical model.
MODEL_NAME = "dicom-characterization"

#: Changed whenever the numbers this module produces change, so two results can
#: be told apart by something other than the date they were produced on.
MODEL_VERSION = "0.1.0"

#: The most findings one result will carry, matching the Application contract.
MAX_FINDINGS = 100

#: Certainty for a fact read straight out of the file, as opposed to a measurement.
_OBSERVED = 1.0


class UnreadableDicomError(Exception):
    """Raised when the bytes supplied are not DICOM this service can read.

    Carries no detail on purpose. The caller is told that the upload could not be
    read and nothing else; the parser's own message names the tag or the offset it
    failed on, and a tag value can be a patient's name.
    """


@dataclass(frozen=True)
class _InstanceFacts:
    """What one instance contributed."""

    study_instance_uid: str | None
    series_instance_uid: str | None
    modality: str | None
    rows: int | None
    columns: int | None
    bits_allocated: int | None
    bits_stored: int | None
    mean_intensity: float | None


def _read_instance(source: BinaryIO) -> _InstanceFacts:
    """Read one DICOM instance and reduce it to the handful of facts reported.

    The dataset does not leave this function. What comes back is a small record
    of primitives, so nothing downstream can reach a tag that was never meant to
    be part of a result.
    """
    try:
        dataset = pydicom.dcmread(source, force=False)
    except InvalidDicomError as error:
        raise UnreadableDicomError from error
    except Exception as error:  # noqa: BLE001 - see below
        # Deliberately broad. pydicom raises a range of exception types on
        # damaged input - struct errors, key errors, decoding errors - and the
        # caller's answer is the same for all of them. `from error` keeps the
        # cause for a local traceback while the handler that turns this into a
        # response never reads it.
        raise UnreadableDicomError from error

    mean = _mean_intensity(dataset)

    return _InstanceFacts(
        study_instance_uid=_text(dataset, "StudyInstanceUID"),
        series_instance_uid=_text(dataset, "SeriesInstanceUID"),
        modality=_text(dataset, "Modality"),
        rows=_integer(dataset, "Rows"),
        columns=_integer(dataset, "Columns"),
        bits_allocated=_integer(dataset, "BitsAllocated"),
        bits_stored=_integer(dataset, "BitsStored"),
        mean_intensity=mean,
    )


def _text(dataset: pydicom.Dataset, keyword: str) -> str | None:
    """Read one string element, or None when it is absent or empty."""
    value = getattr(dataset, keyword, None)

    if value is None:
        return None

    text = str(value).strip()

    return text or None


def _integer(dataset: pydicom.Dataset, keyword: str) -> int | None:
    """Read one integer element, or None when it is absent or unusable."""
    value = getattr(dataset, keyword, None)

    try:
        return int(value) if value is not None else None
    except (TypeError, ValueError):
        return None


def _mean_intensity(dataset: pydicom.Dataset) -> float | None:
    """Return the mean of the instance's pixels, or None when there are none.

    Absent pixel data is an ordinary state - a structured report has none - and
    is not an error. Nor is pixel data this build cannot decode: a missing codec
    means one fewer finding, not a failed request.
    """
    if "PixelData" not in dataset:
        return None

    try:
        pixels = dataset.pixel_array
    except Exception:  # noqa: BLE001 - any decoding failure is "no statistic"
        return None

    try:
        return float(pixels.mean())
    except (TypeError, ValueError):
        return None


def _normalised(mean: float, bits_stored: int | None) -> float:
    """Scale a mean pixel value onto [0, 1].

    Divided by the largest value the stored bit depth can hold, so the number is
    comparable between an 8-bit and a 16-bit image and fits the score range the
    Application contract defines. Clamped, because signed pixel representations
    and rescale intercepts can legitimately put a mean outside the unsigned range.
    """
    depth = bits_stored if bits_stored and bits_stored > 0 else 16
    ceiling = float((1 << depth) - 1)

    return min(1.0, max(0.0, mean / ceiling))


def _distinct(values: list[str | None]) -> list[str]:
    """The distinct non-empty values, in the order they were first seen."""
    seen: list[str] = []

    for value in values:
        if value is not None and value not in seen:
            seen.append(value)

    return seen


def characterise(
    sources: list[BinaryIO],
    study_instance_uid: str | None = None,
    completed_at: datetime | None = None,
) -> StudyAnalysis:
    """Characterise the instances supplied.

    :param sources: One file-like object per DICOM instance.
    :param study_instance_uid: The study CloudFilm authorised, when it supplied
        one. It is a correlation value: this service never fetches anything, so
        the identifier addresses nothing. When given, it is what the result
        reports - the caller's authority is over that study, and a result filed
        under a different one would attribute findings to the wrong patient.
    :param completed_at: Overridable only so a test can pin the timestamp.
    :raises UnreadableDicomError: If any instance cannot be read, or if the
        instances belong to a study other than the one supplied.
    """
    if not sources:
        raise UnreadableDicomError

    facts = [_read_instance(source) for source in sources]

    observed = _distinct([instance.study_instance_uid for instance in facts])

    if study_instance_uid is None:
        if len(observed) != 1:
            # No correlation value, and the files do not agree on one study.
            # There is no honest single value to report.
            raise UnreadableDicomError

        reported_uid = observed[0]
    else:
        if observed and observed != [study_instance_uid]:
            # The bytes are not the study the caller was authorised for. Analysing
            # them anyway would file a finding against the wrong study.
            raise UnreadableDicomError

        reported_uid = study_instance_uid

    findings = _findings(facts)

    return StudyAnalysis(
        study_instance_uid=reported_uid,
        model_name=MODEL_NAME,
        model_version=MODEL_VERSION,
        completed_at_utc=completed_at or datetime.now(timezone.utc),
        findings=findings[:MAX_FINDINGS],
    )


def _findings(facts: list[_InstanceFacts]) -> list[AnalysisFinding]:
    """Reduce the instance facts to the reported findings.

    Aggregate rather than per instance: a study of six hundred slices would
    otherwise produce six hundred findings that say the same thing. Where the
    instances disagree about a value, the finding says so rather than picking one.
    """
    findings: list[AnalysisFinding] = [
        AnalysisFinding(label=f"instances={len(facts)}", score=_OBSERVED),
        AnalysisFinding(
            label=f"series={len(_distinct([item.series_instance_uid for item in facts]))}",
            score=_OBSERVED,
        ),
    ]

    modalities = _distinct([item.modality for item in facts])
    if modalities:
        findings.append(
            AnalysisFinding(label=f"modality={','.join(modalities)}", score=_OBSERVED)
        )

    findings.extend(_geometry(facts))

    means = [item.mean_intensity for item in facts if item.mean_intensity is not None]
    if means:
        depths = [item.bits_stored for item in facts if item.bits_stored is not None]
        mean = sum(means) / len(means)

        findings.append(
            AnalysisFinding(
                label="mean-pixel-intensity-normalised",
                # Rounded so that the same bytes produce the same JSON on every
                # platform, rather than a value that differs in its last bit.
                score=round(_normalised(mean, depths[0] if depths else None), 6),
            )
        )

    return findings


def _geometry(facts: list[_InstanceFacts]) -> list[AnalysisFinding]:
    """Findings for the values that describe the images themselves."""
    findings: list[AnalysisFinding] = []

    for keyword, label in (("rows", "rows"), ("columns", "columns"), ("bits_allocated", "bits-allocated")):
        values = _distinct([_as_text(getattr(item, keyword)) for item in facts])

        if len(values) == 1:
            findings.append(AnalysisFinding(label=f"{label}={values[0]}", score=_OBSERVED))
        elif len(values) > 1:
            # Mixed geometry is a real property of a study with more than one
            # series. Reporting "mixed" is honest; reporting the first value
            # would describe one slice as though it described the study.
            findings.append(AnalysisFinding(label=f"{label}=mixed", score=_OBSERVED))

    return findings


def _as_text(value: int | None) -> str | None:
    """Render an optional integer for use in a label."""
    return None if value is None else str(value)
