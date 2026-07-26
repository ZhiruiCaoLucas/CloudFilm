# Orthanc

Orthanc is CloudFilm's DICOM staging cache. It is deployed unmodified from the
upstream `orthancteam/orthanc` image; CloudFilm only supplies configuration.

## What CloudFilm relies on

| Capability | Why |
| --- | --- |
| DICOMweb plugin | The wire protocol OHIF speaks (QIDO-RS, WADO-RS). |
| PostgreSQL plugin | Replaces the embedded SQLite index so the deployment is backed up with the rest of the database tier. |
| Storage recycling | Bounds how much imaging data ever exists outside the hospital. |

## Network position

Orthanc is attached to the internal Docker network only. It has **no published
port**. Browsers reach DICOMweb exclusively through the OHIF service's reverse
proxy, so the DICOM tier is never addressable from outside the compose network.

## Configuration

`orthanc.json` is mounted read-only at `/etc/orthanc/orthanc.json`. Secrets are
interpolated from the environment (`ORTHANC_PASSWORD`, `ORTHANC_DB_PASSWORD`) and
are never committed.
