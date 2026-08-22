"""What the analysis service will and will not do.

Every DICOM in this suite is synthesised in memory, here, from a handful of
elements. Nothing is read from disk, nothing is committed, and no file in this
repository contains patient data - real or convincingly fake. The patient
identifiers used below are obvious nonsense on purpose: several tests assert that
they do not come back, and an assertion like that is only meaningful if the value
would be unmistakable in a response.
"""

from __future__ import annotations

import io
import json
from pathlib import Path

import numpy
import pydicom
import pytest
from fastapi.testclient import TestClient
from pydicom.dataset import Dataset, FileMetaDataset
from pydicom.uid import CTImageStorage, ExplicitVRLittleEndian, generate_uid

from app.analyzer import MODEL_NAME, MODEL_VERSION
from app.main import app

#: Values that must never appear in a response. Nonsense, so that a match is a
#: finding and not a coincidence.
PATIENT_NAME = "ZZTESTPATIENT^UNMISTAKABLE"
PATIENT_ID = "MRN-UNMISTAKABLE-0001"

STUDY_UID = "1.2.826.0.1.3680043.8.498.10000000000000000000000000000001"
OTHER_STUDY_UID = "1.2.826.0.1.3680043.8.498.10000000000000000000000000000002"


@pytest.fixture(name="client")
def _client() -> TestClient:
    return TestClient(app)


def a_dicom(
    *,
    study_uid: str = STUDY_UID,
    series_uid: str | None = None,
    modality: str = "CT",
    rows: int = 4,
    columns: int = 4,
    fill: int = 100,
    with_pixels: bool = True,
) -> bytes:
    """Build one small DICOM instance in memory.

    Uncompressed, explicit VR little endian, and four by four: large enough to
    have a mean, small enough that a test suite can build dozens without
    noticing.
    """
    meta = FileMetaDataset()
    meta.MediaStorageSOPClassUID = CTImageStorage
    meta.MediaStorageSOPInstanceUID = generate_uid()
    meta.TransferSyntaxUID = ExplicitVRLittleEndian

    dataset = Dataset()
    dataset.file_meta = meta

    # Present precisely so that the tests can prove they do not come back.
    dataset.PatientName = PATIENT_NAME
    dataset.PatientID = PATIENT_ID

    dataset.StudyInstanceUID = study_uid
    dataset.SeriesInstanceUID = series_uid or generate_uid()
    dataset.SOPClassUID = CTImageStorage
    dataset.SOPInstanceUID = meta.MediaStorageSOPInstanceUID
    dataset.Modality = modality

    if with_pixels:
        dataset.Rows = rows
        dataset.Columns = columns
        dataset.BitsAllocated = 16
        dataset.BitsStored = 12
        dataset.HighBit = 11
        dataset.PixelRepresentation = 0
        dataset.SamplesPerPixel = 1
        dataset.PhotometricInterpretation = "MONOCHROME2"
        dataset.PixelData = numpy.full((rows, columns), fill, dtype=numpy.uint16).tobytes()

    buffer = io.BytesIO()
    pydicom.dcmwrite(buffer, dataset, enforce_file_format=True)

    return buffer.getvalue()


def upload(content: bytes, name: str = "instance.dcm") -> dict[str, tuple[str, bytes, str]]:
    """The multipart payload for one instance."""
    return {"files": (name, content, "application/dicom")}


def findings_of(payload: dict) -> dict[str, float]:
    """The findings, as label to score."""
    return {finding["label"]: finding["score"] for finding in payload["findings"]}


# ---------------------------------------------------------------- health ----


def test_health_is_ok(client: TestClient) -> None:
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_health_discloses_nothing_about_the_host(client: TestClient) -> None:
    """A health endpoint reachable by anything on the network is not a place to
    publish versions, paths or package lists."""
    body = response_text(client.get("/health"))

    for leak in ("python", "site-packages", "/usr", "/app", "pydicom", "fastapi"):
        assert leak not in body.lower()


def response_text(response) -> str:
    return response.text


# --------------------------------------------------------------- analyse ----


def test_valid_dicom_is_analysed(client: TestClient) -> None:
    response = client.post("/analyze", files=upload(a_dicom()))

    assert response.status_code == 200


def test_the_reported_study_is_the_one_in_the_dicom(client: TestClient) -> None:
    """With no correlation value supplied, the answer comes from the file."""
    response = client.post("/analyze", files=upload(a_dicom()))

    assert response.json()["studyInstanceUid"] == STUDY_UID


def test_the_model_is_named(client: TestClient) -> None:
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    assert payload["modelName"] == MODEL_NAME
    assert payload["modelName"] == "dicom-characterization"


def test_the_model_is_versioned(client: TestClient) -> None:
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    assert payload["modelVersion"] == MODEL_VERSION
    assert payload["modelVersion"]


def test_findings_are_returned(client: TestClient) -> None:
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    findings = findings_of(payload)

    assert findings
    assert "instances=1" in findings
    assert "modality=CT" in findings
    assert "rows=4" in findings
    assert "columns=4" in findings
    assert "bits-allocated=16" in findings


def test_the_completion_timestamp_matches_the_application_contract(client: TestClient) -> None:
    """`completedAtUtc`, as the .NET record spells it. The two must not drift, or
    the S8.3 adapter will deserialise a default date and nobody will notice."""
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    assert "completedAtUtc" in payload
    assert "analysedAtUtc" not in payload


def test_every_score_is_a_proportion(client: TestClient) -> None:
    """The Application contract defines a score as [0, 1]. A value outside it
    would be rejected by the adapter, or worse, rendered."""
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    for finding in payload["findings"]:
        assert 0.0 <= finding["score"] <= 1.0


# ------------------------------------------------------ pixel statistics ----


def test_pixel_data_produces_a_deterministic_statistic(client: TestClient) -> None:
    """The same bytes, twice, and the same number - normalised against the stored
    bit depth so an 8-bit and a 16-bit image are comparable."""
    content = a_dicom(fill=2048)

    first = findings_of(client.post("/analyze", files=upload(content)).json())
    second = findings_of(client.post("/analyze", files=upload(content)).json())

    # 2048 / (2**12 - 1)
    expected = round(2048 / 4095, 6)

    assert first["mean-pixel-intensity-normalised"] == pytest.approx(expected)
    assert first["mean-pixel-intensity-normalised"] == second["mean-pixel-intensity-normalised"]


def test_an_instance_without_pixels_is_not_an_error(client: TestClient) -> None:
    """A structured report has no pixel data. That is an ordinary state, and it
    costs one finding rather than the request."""
    response = client.post("/analyze", files=upload(a_dicom(with_pixels=False)))

    assert response.status_code == 200
    assert "mean-pixel-intensity-normalised" not in findings_of(response.json())


def test_several_instances_are_summarised_together(client: TestClient) -> None:
    """A study of six hundred slices must not produce six hundred findings that
    all say the same thing."""
    series = generate_uid()
    files = [
        ("files", ("a.dcm", a_dicom(series_uid=series, fill=0), "application/dicom")),
        ("files", ("b.dcm", a_dicom(series_uid=series, fill=4095), "application/dicom")),
    ]

    payload = client.post("/analyze", files=files).json()
    findings = findings_of(payload)

    assert findings["mean-pixel-intensity-normalised"] == pytest.approx(0.5, abs=1e-3)
    assert "instances=2" in findings
    assert "series=1" in findings


def test_mixed_geometry_is_reported_as_mixed(client: TestClient) -> None:
    """Reporting the first slice's dimensions as though they described the study
    would be a quiet lie."""
    files = [
        ("files", ("a.dcm", a_dicom(rows=4, columns=4), "application/dicom")),
        ("files", ("b.dcm", a_dicom(rows=8, columns=8), "application/dicom")),
    ]

    findings = findings_of(client.post("/analyze", files=files).json())

    assert "rows=mixed" in findings
    assert "columns=mixed" in findings


# ------------------------------------------------------------ correlation ---


def test_the_supplied_study_identifier_is_what_is_reported(client: TestClient) -> None:
    """CloudFilm's authority is over the study it names, so that is what the
    result is filed under."""
    response = client.post(
        "/analyze",
        files=upload(a_dicom()),
        data={"studyInstanceUid": STUDY_UID, "requestId": "abc-123"},
    )

    assert response.json()["studyInstanceUid"] == STUDY_UID


def test_bytes_from_another_study_are_refused(client: TestClient) -> None:
    """Analysing them anyway would file findings against the wrong study, which
    is the wrong patient."""
    response = client.post(
        "/analyze",
        files=upload(a_dicom(study_uid=OTHER_STUDY_UID)),
        data={"studyInstanceUid": STUDY_UID},
    )

    assert response.status_code == 400


def test_instances_from_two_studies_are_refused(client: TestClient) -> None:
    """With no correlation value and no agreement between the files, there is no
    honest single answer."""
    files = [
        ("files", ("a.dcm", a_dicom(study_uid=STUDY_UID), "application/dicom")),
        ("files", ("b.dcm", a_dicom(study_uid=OTHER_STUDY_UID), "application/dicom")),
    ]

    assert client.post("/analyze", files=files).status_code == 400


# ---------------------------------------------------------------- refusal ---


def test_an_empty_upload_is_rejected(client: TestClient) -> None:
    response = client.post("/analyze", files=upload(b""))

    assert response.status_code == 400


def test_a_request_with_no_file_is_rejected(client: TestClient) -> None:
    """FastAPI answers 422 for a missing required part. Either way it is refused
    and nothing is parsed."""
    assert client.post("/analyze").status_code == 422


def test_input_that_is_not_dicom_is_rejected(client: TestClient) -> None:
    response = client.post("/analyze", files=upload(b"this is not a DICOM file"))

    assert response.status_code == 400


def test_truncated_dicom_is_rejected(client: TestClient) -> None:
    """A file that starts out plausible and stops halfway - the case a parser is
    most likely to be talkative about."""
    response = client.post("/analyze", files=upload(a_dicom()[:200]))

    assert response.status_code == 400


def test_a_refusal_says_nothing_about_the_parser(client: TestClient) -> None:
    """The message is one of a fixed set. pydicom's own words quote the element
    it failed on, and an element value can be a patient's name."""
    response = client.post("/analyze", files=upload(a_dicom()[:200]))
    body = response.text

    assert response.json() == {"detail": "The upload could not be read as DICOM."}

    for leak in ("Traceback", "pydicom", "File \"", ".py", "0x", "Tag", "VR"):
        assert leak not in body


def test_an_oversized_upload_is_refused(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("ANALYSIS_MAX_UPLOAD_BYTES", "512")

    response = client.post("/analyze", files=upload(a_dicom(rows=64, columns=64)))

    assert response.status_code == 413
    assert response.json() == {"detail": "The upload is larger than this service accepts."}


# -------------------------------------------------------------------- PHI ---


def test_the_patient_name_does_not_come_back(client: TestClient) -> None:
    response = client.post("/analyze", files=upload(a_dicom()))

    assert PATIENT_NAME not in response.text
    assert "ZZTESTPATIENT" not in response.text


def test_the_patient_identifier_does_not_come_back(client: TestClient) -> None:
    response = client.post("/analyze", files=upload(a_dicom()))

    assert PATIENT_ID not in response.text
    assert "MRN" not in response.text


def test_the_response_carries_only_the_agreed_fields(client: TestClient) -> None:
    """A response that grew an extra DICOM tag would leak whatever that tag holds
    for every study, for as long as nobody looked."""
    payload = client.post("/analyze", files=upload(a_dicom())).json()

    assert set(payload) == {
        "studyInstanceUid",
        "modelName",
        "modelVersion",
        "completedAtUtc",
        "findings",
    }

    for finding in payload["findings"]:
        assert set(finding) == {"label", "score"}


def test_a_refusal_carries_no_patient_identity(client: TestClient) -> None:
    response = client.post(
        "/analyze",
        files=upload(a_dicom(study_uid=OTHER_STUDY_UID)),
        data={"studyInstanceUid": STUDY_UID},
    )

    assert PATIENT_NAME not in response.text
    assert PATIENT_ID not in response.text
    assert OTHER_STUDY_UID not in response.text


def test_nothing_is_written_into_the_service_directory(client: TestClient) -> None:
    """The service writes no uploads of its own.

    Scoped to what this application controls. Starlette spools a large upload to
    the operating system's temporary directory, which is the framework's
    behaviour and is documented in the README rather than asserted away here.
    """
    root = Path(__file__).resolve().parent.parent

    before = {path for path in root.rglob("*") if "__pycache__" not in str(path)}

    client.post("/analyze", files=upload(a_dicom(rows=32, columns=32)))

    after = {path for path in root.rglob("*") if "__pycache__" not in str(path)}

    assert after == before


# ------------------------------------------------------------- boundaries ---


def test_the_service_needs_no_authentication(client: TestClient) -> None:
    """No bearer token, no cookie, no API key - and it answers.

    That is the design: authorisation happened in CloudFilm before these bytes
    were fetched. It is also exactly why this service must never be published to
    a host port or reachable from outside the internal network.
    """
    response = client.post("/analyze", files=upload(a_dicom()))

    assert response.status_code == 200
    assert "authorization" not in {name.lower() for name in response.request.headers}


def test_the_service_has_no_route_that_fetches_anything(client: TestClient) -> None:
    """Two endpoints, and neither takes an address.

    A parameter that named a study to retrieve, or carried a URL, would make this
    service able to reach imaging nobody authorised.
    """
    schema = client.get("/openapi.json").json()

    assert set(schema["paths"]) == {"/health", "/analyze"}

    # The names a caller may send, rather than the prose describing them: the
    # documentation is allowed to say "this does not fetch anything", and an
    # assertion over the whole document would forbid explaining the rule.
    body = schema["paths"]["/analyze"]["post"]["requestBody"]["content"]
    accepted = {
        name.lower()
        for media in body.values()
        for name in _properties_of(schema, media["schema"])
    }

    assert accepted == {"files", "studyinstanceuid", "requestid"}

    assert schema["paths"]["/analyze"]["post"].get("parameters", []) == []
    assert schema["paths"]["/health"]["get"].get("parameters", []) == []

    for name in accepted:
        for forbidden in ("url", "uri", "orthanc", "retrieve", "host", "path", "callback"):
            assert forbidden not in name


def _properties_of(schema: dict, reference: dict) -> list[str]:
    """The property names of a possibly-referenced request body schema."""
    target = reference

    if "$ref" in reference:
        name = reference["$ref"].rsplit("/", 1)[-1]
        target = schema["components"]["schemas"][name]

    return list(target.get("properties", {}))


def test_the_project_depends_on_nothing_that_could_call_out() -> None:
    """The dependency list is the other half of that guarantee.

    No HTTP client, no Orthanc library, no ML framework. A service that cannot
    make an outbound request cannot be turned into one that fetches a study, and
    a first slice that cannot be talked into downloading a model is one a hospital
    can run offline.
    """
    root = Path(__file__).resolve().parent.parent

    # Requirements only, not the comments explaining why they are absent.
    requirements = "\n".join(
        line.split("#", 1)[0].strip().lower()
        for line in (root / "requirements.txt").read_text().splitlines()
    )

    for forbidden in (
        "requests",
        "httpx",
        "aiohttp",
        "urllib3",
        "orthanc",
        "torch",
        "tensorflow",
        "onnxruntime",
        "transformers",
        "nvidia",
    ):
        assert forbidden not in requirements
