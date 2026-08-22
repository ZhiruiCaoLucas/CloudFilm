"""The CloudFilm analysis service.

An internal service with two endpoints and no idea who CloudFilm's users are. It
receives DICOM that the CloudFilm API has already decided the caller may see, and
it answers with a characterisation of those bytes.

What it deliberately cannot do:

* reach Orthanc, the Retrieve Service, or anything else - it makes no outbound
  calls at all, so a study identifier it is given addresses nothing;
* authenticate or authorise anybody - authorisation happened in the API, before
  the bytes were fetched, and a second implementation of that decision would be
  one that drifts;
* keep anything - no database, no cache, no files it writes on purpose.

It is inside the PHI boundary, because DICOM carries patient identifiers in its
own tags whatever the request envelope says. It must be deployed and operated
accordingly: internal network only, no published port, and nothing in its logs.
"""

from __future__ import annotations

import logging
from typing import Annotated, BinaryIO

from fastapi import Depends, FastAPI, File, Form, HTTPException, Request, UploadFile, status
from fastapi.responses import JSONResponse

from .analyzer import MODEL_NAME, MODEL_VERSION, UnreadableDicomError, characterise
from .config import READ_CHUNK_BYTES, max_upload_bytes
from .models import HealthStatus, ServiceError, StudyAnalysis

#: Logs carry the shape of a request and never its contents. No dataset, no tag,
#: no filename, no identifier that could be a patient's - a log line here is
#: written to a hospital's aggregated logging, which is not inside this service's
#: boundary even when this service is inside the hospital's.
_log = logging.getLogger("cloudfilm.analysis")

#: The one message a rejected upload receives, whatever was wrong with it.
_UNREADABLE = "The upload could not be read as DICOM."

#: The one message an oversized upload receives.
_TOO_LARGE = "The upload is larger than this service accepts."

#: 413. Written as an integer rather than taken from `status`, whose constant for
#: it has been renamed once already; a status code is stable in a way that a
#: framework's spelling of it is not.
_STATUS_TOO_LARGE = 413

app = FastAPI(
    title="CloudFilm Analysis Service",
    version=MODEL_VERSION,
    summary="Non-clinical characterisation of DICOM supplied by the CloudFilm API.",
)


@app.get("/health", response_model=HealthStatus, summary="Liveness.")
def health() -> HealthStatus:
    """Report that the process is up.

    A constant, and nothing else. Versions, paths, package lists and environment
    values would make an unauthenticated endpoint into an inventory of the host.
    """
    return HealthStatus(status="ok")


@app.exception_handler(UnreadableDicomError)
async def _unreadable(request: Request, exception: UnreadableDicomError) -> JSONResponse:
    """Answer a parse failure without repeating what the parser said.

    The exception's cause is not read, logged or serialised. pydicom's messages
    routinely quote the element that failed, and an element value can be a
    patient's name.
    """
    del request, exception

    return JSONResponse(
        status_code=status.HTTP_400_BAD_REQUEST,
        content=ServiceError(detail=_UNREADABLE).model_dump(by_alias=True),
    )


def _require_within_limit(request: Request) -> None:
    """Refuse an upload that announces itself as too large.

    A first, cheap gate on the declared length, so an oversized body is refused
    before any of it is read. It is not the only gate - a caller can lie, or send
    no length at all - and the read below counts what actually arrives.
    """
    declared = request.headers.get("content-length")

    if declared is None:
        return

    try:
        length = int(declared)
    except ValueError:
        return

    if length > max_upload_bytes():
        raise HTTPException(
            status_code=_STATUS_TOO_LARGE,
            detail=_TOO_LARGE,
        )


def _read_within_limit(uploads: list[UploadFile]) -> list[BinaryIO]:
    """Read every upload, stopping if the total exceeds the limit.

    Counted across the whole request rather than per file, so a caller cannot get
    round the limit by splitting one study into a hundred parts.

    Returns the file objects the parser will read. They are the objects Starlette
    already holds; see the README on what Starlette does with them, which is the
    one part of this service's "nothing touches disk" claim that is the
    framework's to keep rather than ours.
    """
    limit = max_upload_bytes()
    total = 0

    for upload in uploads:
        while True:
            chunk = upload.file.read(READ_CHUNK_BYTES)

            if not chunk:
                break

            total += len(chunk)

            if total > limit:
                raise HTTPException(
                    status_code=_STATUS_TOO_LARGE,
                    detail=_TOO_LARGE,
                )

        upload.file.seek(0)

    if total == 0:
        raise UnreadableDicomError

    return [upload.file for upload in uploads]


@app.post(
    "/analyze",
    response_model=StudyAnalysis,
    summary="Characterises the DICOM supplied with the request.",
    responses={
        400: {"model": ServiceError, "description": "The upload is not readable DICOM."},
        413: {"model": ServiceError, "description": "The upload is too large."},
    },
)
async def analyze(
    files: Annotated[list[UploadFile], File(description="One or more DICOM instances.")],
    _: Annotated[None, Depends(_require_within_limit)] = None,
    study_instance_uid: Annotated[str | None, Form(alias="studyInstanceUid")] = None,
    request_id: Annotated[str | None, Form(alias="requestId")] = None,
) -> StudyAnalysis:
    """Characterise one study's DICOM.

    The bytes arrive with the request. There is no identifier to resolve and no
    address to fetch, which is what keeps this service unable to reach imaging
    nobody authorised: the only study it can see is the one it was handed.

    `studyInstanceUid` and `requestId` are correlation values from CloudFilm.
    Neither causes a retrieval. The study identifier, when supplied, is what the
    result is filed under, and instances belonging to a different study are
    refused rather than analysed under the wrong one.
    """
    sources = _read_within_limit(files)

    analysis = characterise(sources, study_instance_uid=study_instance_uid)

    # Counts and a correlation value. Not the study, not a tag, not a filename.
    _log.info(
        "analysed request=%s instances=%d findings=%d",
        _safe_correlation(request_id),
        len(files),
        len(analysis.findings),
    )

    return analysis


def _safe_correlation(request_id: str | None) -> str:
    """Reduce a caller-supplied correlation value to something safe to log.

    Bounded and stripped of anything that is not an identifier character, so a
    caller cannot inject newlines into a log line - or smuggle a patient's name
    into one through a field nobody checks.
    """
    if not request_id:
        return "-"

    cleaned = "".join(character for character in request_id if character.isalnum() or character in "-_")

    return cleaned[:64] or "-"


__all__ = ["MODEL_NAME", "MODEL_VERSION", "app"]
