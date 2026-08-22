# CloudFilm Analysis Service

A small FastAPI service that characterises DICOM handed to it by the CloudFilm
API.

> **Not clinical.** This service does not diagnose, screen, triage or grade
> anything. It reports what a DICOM file says about itself — modality, geometry,
> bit depth, how many instances arrived — plus one reproducible pixel statistic.
> No output is a medical finding, and nothing here is a medical device.

> **Not production ready.** This is the first slice. It runs, it is tested, and
> it is deliberately narrow.

## Where it sits

```
Browser → CloudFilm API ─┬─ IStudyAccessPolicy      authorise the study
                         ├─ IStudyDownloadSource → Retrieve → Orthanc
                         └─ POST /analyze ────────► this service
```

The API authorises the request and fetches the bytes. This service receives them.
It has no idea who CloudFilm's users are and never decides whether anyone may see
a study.

**It must run on the internal network with no published host port.** It
authenticates nobody, so anything that can reach it can have DICOM processed by
it. That is acceptable exactly as long as nothing outside the compose network can.

What it cannot do, by construction rather than by policy:

- reach Orthanc, the Retrieve Service, or any other address — it makes no
  outbound calls and depends on no HTTP client;
- fetch a study from an identifier — the study identifier it accepts is a
  correlation value, and there is nothing behind it to fetch;
- accept a URL — there is no such field;
- authenticate, authorise, or read a CloudFilm token;
- write DICOM to disk on purpose, or keep anything between requests.

## Endpoints

### `GET /health`

```json
{ "status": "ok" }
```

A constant. No versions, no paths, no package list, no environment.

### `POST /analyze`

`multipart/form-data`:

| Part | Required | Meaning |
| --- | --- | --- |
| `files` | yes | One or more DICOM instances. Repeat the part for several. |
| `studyInstanceUid` | no | The study CloudFilm authorised. Correlation only. |
| `requestId` | no | Correlation only. Sanitised and bounded before it is logged. |

When `studyInstanceUid` is supplied it is what the result is filed under, and
instances belonging to a different study are **refused** — analysing them would
attribute findings to the wrong patient. When it is absent, the study identifier
comes from the files, and files that disagree are refused for the same reason.

**200** — the analysis. Field names are camelCase, matching the CloudFilm
`StudyAnalysis` contract:

```json
{
  "studyInstanceUid": "1.2.826.0.1.3680043.8.498...",
  "modelName": "dicom-characterization",
  "modelVersion": "0.1.0",
  "completedAtUtc": "2026-08-16T09:00:00Z",
  "findings": [
    { "label": "instances=2", "score": 1.0 },
    { "label": "series=1", "score": 1.0 },
    { "label": "modality=CT", "score": 1.0 },
    { "label": "rows=512", "score": 1.0 },
    { "label": "columns=512", "score": 1.0 },
    { "label": "bits-allocated=16", "score": 1.0 },
    { "label": "mean-pixel-intensity-normalised", "score": 0.500122 }
  ]
}
```

**400** — `{"detail": "The upload could not be read as DICOM."}` for an empty
upload, something that is not DICOM, a truncated file, or instances that do not
belong to the study named. One message for all of them.

**413** — `{"detail": "The upload is larger than this service accepts."}`

**422** — FastAPI's own answer when the `files` part is missing entirely.

#### Why the values are in the labels

The CloudFilm contract gives a finding two fields: a `label` and a `score`
constrained to `[0, 1]`. Structural facts are not probabilities, so a fact is
stated in the label (`rows=512`) with a score of `1.0` meaning "observed", and
the one genuine measurement — mean pixel intensity, divided by the largest value
the stored bit depth can hold — uses the score as a score. It reads oddly. If a
later sprint gives `AnalysisFinding` a typed value, this is the first thing that
should change.

`completedAtUtc` matches the .NET record. The sprint brief called it
`analysedAtUtc`; the .NET record spells it the other way and is what the adapter
deserialises, so the field keeps the name the contract gave it. Tests on both
sides pin the spelling.

## PHI

DICOM carries patient identifiers in its own tags, so **this service is inside
the PHI boundary** whatever the request envelope says. It is operated
accordingly:

- nothing about a dataset is logged — no tag, no name, no identifier, no
  filename. A log line carries a sanitised `requestId`, an instance count and a
  finding count;
- parser messages are never passed on. pydicom names the element it failed on,
  and an element value can be a patient's name, so every failure becomes the same
  fixed sentence;
- the response carries only the fields above. Arbitrary DICOM metadata is never
  echoed;
- nothing is written to disk by the application, and nothing is kept between
  requests.

### The one caveat, stated rather than glossed

**Starlette spools large uploads to the operating system's temporary directory.**
`UploadFile` is a `SpooledTemporaryFile`: small bodies stay in memory, and past
the framework's spool threshold it writes to a temp file. This service never
does that itself and never writes into its own directory — a test asserts the
latter — but the framework's spooling is real and is not something an application
can turn off cleanly.

Until a later sprint addresses it, treat it as a deployment concern:

- mount the container's temp directory as `tmpfs` so spooled bytes never reach
  a real disk;
- keep the upload limit low enough that spooling is rare;
- do not run this image on a host whose temp directory is shared or persisted.

Do not read "no DICOM is persisted" as an end-to-end guarantee. It is true of
this application's own code and no more than that.

## Configuration

| Variable | Default | Meaning |
| --- | --- | --- |
| `ANALYSIS_MAX_UPLOAD_BYTES` | `67108864` (64 MiB) | Largest upload accepted, counted across every part of one request. |

There is no other configuration. No token, no key, no database, no cache, no
Orthanc credential, no Retrieve address — the service needs none of them, and
each one that does not exist is one that cannot be set wrongly.

The limit is enforced twice: the declared `Content-Length` is refused up front,
and the bytes are counted as they are read, so a caller that understates the
length is still stopped. A real study can exceed 64 MiB; raising the limit is a
deliberate act that should be taken together with the spooling caveat above.

## Running locally

```bash
cd apps/analysis

python -m venv .venv && source .venv/bin/activate
pip install -r requirements-dev.txt

uvicorn app.main:app --host 127.0.0.1 --port 8000
```

`127.0.0.1` on purpose. There is no reason for this to listen on anything else on
a developer's machine.

```bash
curl -s http://127.0.0.1:8000/health

curl -s -X POST http://127.0.0.1:8000/analyze \
  -F "files=@instance.dcm;type=application/dicom" \
  -F "studyInstanceUid=1.2.826..." \
  -F "requestId=local-1"
```

## Tests

```bash
cd apps/analysis
pip install -r requirements-dev.txt
pytest
```

Every DICOM in the suite is synthesised in memory. No fixture file, no sample
study, and no patient data — real or realistic — is committed to this repository.

## Docker

```bash
# From the repository root, because the build context includes apps/analysis.
docker build -f apps/analysis/docker/analysis.Dockerfile -t cloudfilm-analysis .

# Local smoke test only. In the CloudFilm stack this service publishes nothing.
docker run --rm -p 127.0.0.1:8000:8000 cloudfilm-analysis
```

The image runs as a non-root user and listens on `0.0.0.0:8000` **inside** the
container. In the CloudFilm stack it is the `analysis` service, on the `internal`
network with no `ports:` key at all:

```bash
docker compose up -d --build analysis
docker compose ps analysis
```

## Not yet done

- No service-to-service authentication. There is none between the API and the
  Retrieve Service either; isolation is the control, and adding a second
  mechanism here would need a decision, not an afternoon.
- No persistence. A result is returned and forgotten; nothing stores it, and
  whether an automated observation is a record worth keeping is a product
  question rather than an engineering one.

## Already wired in

The CloudFilm API calls this service. `StudyAnalyser` in
`CloudFilm.Infrastructure` implements the `IStudyAnalyser` port, and
`DicomFormDataContent` rewrites the archive's WADO-RS `multipart/related` body
into the `multipart/form-data` upload this service accepts - one instance at a
time, streaming, so a whole study is never held in memory.

In the Compose stack the service is `analysis`, on the `internal` network with no
published port, and the API reaches it at `http://analysis:8000` through
`Downstream__AnalysisServiceUrl`.
