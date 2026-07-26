# Running the viewer locally

How to get a real CT or CBCT study on screen in the CloudFilm workspace, using
Orthanc and OHIF on your own machine.

Local development only. Nothing here touches a hospital system, and the
production stack is unchanged by it.

---

## Why a seeding step exists

The portal opens a study by handing OHIF a **Study Instance UID**. Those UIDs are
minted by whatever produced the DICOM, so the built-in fixtures cannot guess
them — their UIDs are synthetic and no viewer can resolve them.

`pnpm seed:orthanc` closes that gap. It uploads DICOM to Orthanc, reads back
what Orthanc actually stored, and writes the real UIDs into
`packages/shared/mock/seeded-studies.json`. The mock layer prefers that file
over its built-in fixtures, so the study list becomes a list of studies the
viewer can genuinely open.

```
services/orthanc/seed/*.dcm
        │  pnpm seed:orthanc
        ▼
     Orthanc  ──read back──►  packages/shared/mock/seeded-studies.json
        ▲                              │
        │ /dicom-web                   │ real Study Instance UIDs
   OHIF nginx  ◄── iframe ──  CloudFilm workspace
```

---

## Steps

### 1. Provide a study

Drop DICOM files anywhere under:

```
services/orthanc/seed/
```

A whole directory tree is fine. The contents are git-ignored — sample studies
are patient-shaped data and large, so they are never committed.

Public CT and CBCT sample studies are published by the Orthanc project, the OHIF
project, and The Cancer Imaging Archive. Alternatively point the script at a zip:

```bash
CLOUDFILM_SAMPLE_DICOM_URL=https://example.org/sample.zip pnpm seed:orthanc
```

### 2. Start the stack

The **development overlay is required**. It publishes Orthanc on 8042 so the
seed script can reach it, and turns Orthanc's authentication off so the viewer's
proxy can read DICOMweb without a credential.

```bash
./scripts/generate-secrets.sh          # first run only
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
docker compose ps
```

### 3. Seed

```bash
pnpm seed:orthanc
```

It prints the Medical Record Numbers it loaded.

### 4. Restart the portal and open a study

```bash
docker compose restart portal          # or restart `pnpm dev`
```

Search the MRN the script printed, then press **Open Study**.

Expected: the workspace header names the patient, the navigator lists the
studies, the status bar reads **Viewer Online**, and OHIF renders the series.
Pan, zoom and window/level are OHIF's own tools and work as soon as pixels load.

---

## Ports

| Port | Service |
| --- | --- |
| 3000 | Portal |
| 8080 | API |
| 8081 | OHIF Viewer |
| 8082 | Retrieve Service |
| 8042 | Orthanc — dev overlay only |

---

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Status bar stays **Viewer Offline** | Nothing is serving on `CLOUDFILM_VIEWER_URL`. | `docker compose ps`; confirm `ohif` is up and 8081 is published. Press **Check again** — no reload needed. |
| Viewer loads, study is empty | The UID in the URL is not in Orthanc. | You are on built-in fixtures. Run `pnpm seed:orthanc`, then restart the portal so it re-reads the file. |
| Viewer shows its study list instead of your study | Same cause; the study list is the deliberate fallback. | As above. Browse from the list to confirm Orthanc has the data. |
| `pnpm seed:orthanc` cannot reach Orthanc | Started without the dev overlay, so 8042 is not published. | Restart with both compose files. |
| Orthanc accepted 0 files | The files are not DICOM, or are DICOMDIR/JPEG. | Check with a DICOM tool; upload the individual `.dcm` instances. |
| `/dicom-web` returns 401 | Orthanc authentication is on — you are not using the dev overlay. | Restart with both compose files. |
| `orthanc` container never reports healthy | Its health check shells out to `curl`, which the image may not carry. | The dev overlay already starts `ohif` and `retrieve` on `service_started` rather than health, so this should not block you. Check `docker compose logs orthanc`. |
| 3D / volume rendering unavailable | OHIF needs `SharedArrayBuffer`, which needs cross-origin isolation across both origins. | Expected. 2D review is unaffected; see the note in `services/ohif/default.conf`. |

---

## Returning to fixtures

```bash
git checkout packages/shared/mock/seeded-studies.json
```

The portal falls back to its built-in fixtures whenever that file has no
records, which is also why a fresh clone with no Docker still runs.
