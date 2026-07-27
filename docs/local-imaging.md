# Getting a real study on screen

Five steps, each independently verifiable. **Do not move to the next step until
the current one produces the evidence shown.** A later check cannot mean
anything while an earlier one is broken.

```
Step 1  Orthanc is running
Step 2  Orthanc contains studies
Step 3  OHIF is served and can reach DICOMweb
Step 4  The portal iframe loads OHIF
Step 5  Open Study opens the correct Study Instance UID
```

Most of this is automated:

```bash
pnpm verify:imaging            # all steps, stops at the first failure
pnpm verify:imaging --step 2   # one step
```

The verifier prints the evidence it used — status codes, study counts, UIDs — so
its output is proof rather than a summary. Steps 4 and 5 finish with a visual
check that no command can make for you.

> **Nothing here is assumed to work.** The container image tags, config mount
> paths and Orthanc environment mapping were written from documentation, not
> from a running system. Each step below names the specific thing it is testing
> and what to do when that assumption turns out to be wrong.

---

## Step 1 — Orthanc is running

**Run**

```bash
./scripts/generate-secrets.sh    # first run only
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
docker compose ps
curl -i http://localhost:8042/system
```

The dev overlay is required: it publishes port 8042 and disables Orthanc's
authentication so the viewer proxy can read DICOMweb.

**Expected**

`docker compose ps` lists `orthanc` as running, and:

```
HTTP/1.1 200 OK
{"ApiVersion":...,"DatabaseVersion":...,"Name":"CloudFilm Orthanc","Version":"1.12.x"}
```

**Return this evidence**

```bash
docker compose ps
curl -i http://localhost:8042/system
docker compose logs --tail 40 orthanc
```

**Diagnosing failure**

| Symptom | Cause | Fix |
| --- | --- | --- |
| `manifest unknown` / `pull access denied` on `up` | `ORTHANC_IMAGE` tag does not exist. This is a guess in `.env.example`. | `docker search orthancteam/orthanc`, pick a real tag, set `ORTHANC_IMAGE` in `.env`. |
| Container exits immediately | Orthanc could not parse its configuration. | `docker compose logs orthanc`. Both `orthanc.json` and `zz-dev.json` are mounted; the log names the offending file. |
| `curl: connection refused` | Port 8042 is not published — the dev overlay was not applied. | Restart with **both** `-f` files. |
| Container restarts in a loop | Usually the PostgreSQL plugin cannot reach the database. | `docker compose logs orthanc \| grep -i postgres`; confirm `postgres` is healthy. |
| `orthanc` never becomes healthy | Its health check shells out to `curl`, which the image may not carry. | Not blocking: the dev overlay starts `ohif` and `retrieve` on `service_started`. Report the log and I will replace the check. |
| **Logs stop at `generating temporary configuration file in /tmp/orthanc.json`, port 8042 never opens** | The container is *hung*, not crashed. Almost always the PostgreSQL plugin retrying a connection it can never complete. | See "Orthanc hangs after generating its configuration" below. |

### Orthanc hangs after generating its configuration

The image merges `/etc/orthanc/*.json` with every `ORTHANC__*` environment
variable into `/tmp/orthanc.json`, then starts Orthanc with it. If the log stops
there, Orthanc *did* start — it is blocked before opening its HTTP port.

Collect everything at once:

```bash
./scripts/diagnose-orthanc.sh > orthanc-evidence.txt 2>&1
```

The two decisive sections are **generated configuration /tmp/orthanc.json** —
the ground truth for what Orthanc actually received — and **can orthanc
authenticate against postgres**.

#### Known cause: the Orthanc role password did not match

`scripts/init-databases.sh` runs inside the *postgres* container and used to read
`ORTHANC__POSTGRESQL__PASSWORD`, a variable defined only on the *orthanc*
service. It was therefore always unset there, a silent fallback won, and the role
was created with a different password from the one Orthanc connects with. The
PostgreSQL plugin then retried forever and the HTTP port never opened.

Fixed: both services now receive `ORTHANC_DB_PASSWORD`, and the script fails
loudly rather than defaulting.

**The fix does not apply itself to an existing volume.** Scripts in
`/docker-entrypoint-initdb.d` run only when the data directory is empty, so a
database created before the fix still has the old password. Either repair it:

```bash
docker compose exec postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
   "ALTER ROLE orthanc WITH LOGIN PASSWORD '"'"'$ORTHANC_DB_PASSWORD'"'"'"'
docker compose restart orthanc
```

or start from a clean volume — safe in development, it discards local data only:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

#### If the password was not the cause

Work through `/tmp/orthanc.json` from the evidence file:

| Look for | Meaning |
| --- | --- |
| No `PostgreSQL` block | The plugin is inert; Orthanc is on SQLite and would have started. The hang is elsewhere. |
| `PostgreSQL` present, plugin missing from the plugin listing | The image gates plugins behind its own environment variables; the config block is being ignored. |
| Settings from `zz-dev.json` absent | The image does not merge every file in `/etc/orthanc`. `AuthenticationEnabled` would still be `true`, which also breaks Step 3. |
| Nothing listening on 8042 and no `ps` entry for Orthanc | The process died without logging; check `docker compose logs orthanc` for a segfault. |

---

## Step 2 — Orthanc contains studies

Orthanc starts empty. Nothing can be displayed until DICOM is imported.

**Run**

```bash
# put one or more .dcm files anywhere under services/orthanc/seed/ first
pnpm seed:orthanc
pnpm verify:imaging --step 2
```

**Expected**

```
  PASS  GET http://localhost:8042/studies?expand -> 200, 1 study(ies)
        1.3.6.1.4.1.14519...  20240318  LIDC-0001  CHEST CT W CONTRAST
```

**Return this evidence**

```bash
pnpm seed:orthanc
curl -s http://localhost:8042/studies | head
pnpm verify:imaging --step 2
```

**Diagnosing failure**

| Symptom | Cause | Fix |
| --- | --- | --- |
| `0 studies` after seeding | Orthanc rejected the uploads. | `curl -i -X POST http://localhost:8042/instances --data-binary @one-file.dcm` — the body says why. |
| `accepted 0, rejected N` | The files are not DICOM instances (a DICOMDIR, JPEGs, or a zip). | Upload individual `.dcm` files. |
| `401` from `/studies` | Authentication is on, so the dev overlay is not applied. | Restart with both compose files. |
| Script cannot reach Orthanc | Port 8042 not published. | Same as above. |

---

## Step 3 — OHIF is served and can reach DICOMweb

This is the step most likely to fail, because it tests three unverified
assumptions at once: that the image serves from `/usr/share/nginx/html`, that it
reads nginx config from `/etc/nginx/conf.d/default.conf`, and that Orthanc's
DICOMweb plugin is loaded.

**Run**

```bash
pnpm verify:imaging --step 3
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/
curl -s http://localhost:8081/dicom-web/studies | head -c 400
```

**Expected**

```
  PASS  GET http://localhost:8081/ -> 200, NNNNN bytes
  PASS  GET http://localhost:8081/app-config.js -> 200, CloudFilm data source present
  PASS  GET http://localhost:8081/dicom-web/studies -> 200, 1 study(ies) via QIDO-RS
        0020000D = 1.3.6.1.4.1.14519...
```

The `0020000D` value must equal a Study Instance UID from Step 2.

**Return this evidence**

```bash
pnpm verify:imaging --step 3
docker compose logs --tail 40 ohif
docker compose exec ohif ls -l /usr/share/nginx/html/app-config.js
docker compose exec ohif cat /etc/nginx/conf.d/default.conf | head -20
```

**Diagnosing failure**

| Symptom | Cause | Fix |
| --- | --- | --- |
| `/` refused, container exited | nginx rejected the mounted config, or the mount path is wrong for this image. | `docker compose logs ohif`. If the path is wrong, correct the `ohif` volume in `docker-compose.yml`. |
| `/app-config.js -> 404` | The image serves from a different document root. | `docker compose exec ohif sh -c 'ls /usr/share/nginx/html'`. Adjust the mount to the real root. |
| `/dicom-web/studies -> 404` | The proxy location is not active, or Orthanc's DICOMweb plugin is not loaded. | `curl -i http://localhost:8042/dicom-web/studies` — if that 404s too, the plugin is the problem: `docker compose logs orthanc \| grep -i dicomweb`. |
| `/dicom-web/studies -> 401` | Orthanc still wants credentials. | The dev overlay is not applied, or `zz-dev.json` did not load. `docker compose exec orthanc ls /etc/orthanc`. |
| `/dicom-web/studies -> 502` | nginx cannot resolve `orthanc`. | Both containers must share the `internal` network. `docker compose exec ohif getent hosts orthanc`. |
| 200 but `0 studies` | DICOMweb works, Orthanc is empty. | Return to Step 2. |

---

## Step 4 — The portal iframe loads OHIF

No command can confirm an iframe painted. This step is visual.

**Run**

```bash
pnpm verify:imaging --step 4
```

Then open <http://localhost:3000/> in a browser, search the record number
Step 2 printed, and press **Open Study**.

**Expected**

- The status bar bottom-left reads **Viewer Online**.
- The reading area shows OHIF, not the "Viewer Offline" panel.
- The browser console shows no `ERR_CONNECTION_REFUSED` for port 8081.

**Return this evidence**

- A screenshot of the workspace.
- The browser devtools **Console** tab, and the **Network** tab filtered to
  `dicom-web`.

**Diagnosing failure**

| Symptom | Cause | Fix |
| --- | --- | --- |
| Status bar stays **Viewer Offline** | The browser cannot reach `CLOUDFILM_VIEWER_URL`. The portal probes it before mounting the iframe, which is why you get this panel and never a browser error page. | Confirm <http://localhost:8081> opens directly. Press **Check again** — no reload needed. |
| **No imaging data for this study** | Correct behaviour: the study is a placeholder. | Step 2 and Step 5. |
| Iframe is blank white | OHIF loaded but crashed on start-up. | Browser console inside the frame. Usually `app-config.js` is malformed. |
| Iframe refuses to display | The viewer origin is sending a framing restriction. | `curl -I http://localhost:8081/` and look for `X-Frame-Options` or `Content-Security-Policy: frame-ancestors`. Our config sets neither, so it would be coming from the image. |

---

## Step 5 — Open Study opens the correct Study Instance UID

**Run**

```bash
pnpm verify:imaging --step 5
```

**Expected**

```
  PASS  all N portal UID(s) exist in Orthanc
  Open these to finish verification by eye:
        workspace: http://localhost:3000/workspace/1.3.6...
        viewer:    http://localhost:8081/viewer?StudyInstanceUIDs=1.3.6...
```

Open both. They must show the **same** study.

**Expected in the workspace**

- Header names the patient from Step 2.
- Status bar shows that Study UID and a non-zero image count.
- The CT renders, and OHIF's own tools work: drag to pan, scroll to zoom,
  right-drag for window/level.

**Return this evidence**

- A screenshot of the rendered CT.
- The status bar, legible.
- `pnpm verify:imaging` output in full.

**Diagnosing failure**

| Symptom | Cause | Fix |
| --- | --- | --- |
| `seeded-studies.json contains no studies` | The portal is on built-in placeholders. | `pnpm seed:orthanc`, then restart the portal so it re-reads the file. |
| `N portal UID(s) are not in Orthanc` | Fixtures are stale — Orthanc was wiped or reseeded. | `pnpm seed:orthanc` again, then restart the portal. |
| Workspace correct, viewer empty | OHIF resolved the UID but found no series. | Compare `curl -s "http://localhost:8081/dicom-web/studies?StudyInstanceUID=<uid>"` with what Orthanc reports. |
| Study opens, no image | Transfer syntax the browser cannot decode. | `docker compose logs orthanc`; try an uncompressed sample. |
| Pan/zoom/window-level do nothing | OHIF's own tools; the portal is not involved. | Confirm at <http://localhost:8081> directly. If broken there too, it is an OHIF configuration issue. |

---

## Returning to placeholders

```bash
git checkout packages/shared/mock/seeded-studies.json
```

The portal falls back to built-in fixtures whenever that file has no records,
which is why a fresh clone with no Docker still runs.

## Ports

| Port | Service |
| --- | --- |
| 3000 | Portal |
| 8080 | API |
| 8081 | OHIF Viewer |
| 8082 | Retrieve Service |
| 8042 | Orthanc — dev overlay only |
