# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm Analysis Service
#
# Internal only. This image publishes no port and is not meant to be reachable
# from a host: it accepts DICOM that the CloudFilm API has already authorised,
# and it authenticates nobody. Reachable from outside the compose network it
# would be an unauthenticated way to have imaging processed.
#
# CPU only. No CUDA base, no GPU runtime, no deep learning stack - the service
# characterises DICOM and runs no model, and an image carrying one would be
# gigabytes of attack surface a hospital has to patch for nothing.
#
# Build from the repository root:
#   docker build -f apps/analysis/docker/analysis.Dockerfile -t cloudfilm-analysis .
# ---------------------------------------------------------------------------

FROM python:3.12-slim-bookworm AS build

# Dependencies into a virtual environment of their own, so the runtime stage can
# take the environment and leave pip, its cache and the build tooling behind.
ENV PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /src

COPY apps/analysis/requirements.txt ./

RUN python -m venv /opt/venv \
    && /opt/venv/bin/pip install --no-cache-dir -r requirements.txt

# ---------------------------------------------------------------------------

FROM python:3.12-slim-bookworm AS runtime

# PYTHONDONTWRITEBYTECODE: the application directory stays read-only in practice,
# and .pyc files written beside the source are noise in an image nobody rebuilds.
# PYTHONUNBUFFERED: logs arrive when they happen rather than when a buffer fills,
# which is what makes `docker compose logs` usable during an incident.
ENV PATH=/opt/venv/bin:$PATH \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

# Non-root, and without a login shell or a home directory to write into. The
# service reads a request and answers it; it has no reason to own anything.
RUN groupadd --system --gid 10001 cloudfilm \
    && useradd --system --uid 10001 --gid cloudfilm --no-create-home --shell /usr/sbin/nologin cloudfilm

WORKDIR /app

COPY --from=build /opt/venv /opt/venv
COPY --chown=root:root apps/analysis/app ./app

USER cloudfilm

# Documentation, not publication. Compose decides what is reachable, and for this
# service the answer is "only the internal network".
EXPOSE 8000

# uvicorn directly, with no reloader and no development server. One worker: the
# service holds a study in memory while it reads it, and several workers on one
# small container is a way to be killed by the OOM reaper under load rather than
# to be faster.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1", "--no-server-header"]
