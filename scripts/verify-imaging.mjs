#!/usr/bin/env node
/**
 * Step-gated verification of the local imaging stack.
 *
 * Each step proves one link in the chain that ends with a real DICOM study on
 * screen. Steps run in order and stop at the first failure, because a later
 * check cannot mean anything while an earlier one is broken.
 *
 * Every assertion prints the evidence it used - status codes, counts, UIDs - so
 * the output can be pasted somewhere as proof rather than summarised as "works".
 *
 *   node scripts/verify-imaging.mjs           run every step
 *   node scripts/verify-imaging.mjs --step 2  run one step
 *
 * Environment:
 *   ORTHANC_URL   default http://localhost:8042
 *   VIEWER_URL    default http://localhost:8081
 *   PORTAL_URL    default http://localhost:3000
 */

import { readFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const FIXTURE_FILE = join(REPO_ROOT, 'packages', 'shared', 'mock', 'seeded-studies.json');

const ORTHANC_URL = trimUrl(process.env['ORTHANC_URL'] ?? 'http://localhost:8042');
const VIEWER_URL = trimUrl(process.env['VIEWER_URL'] ?? 'http://localhost:8081');
const PORTAL_URL = trimUrl(process.env['PORTAL_URL'] ?? 'http://localhost:3000');

const REQUEST_TIMEOUT_MS = 10_000;

function trimUrl(value) {
  return value.replace(/\/+$/, '');
}

/* ------------------------------------------------------------------ output */

const state = { failures: 0 };

function heading(number, title) {
  console.info(`\n── Step ${number}: ${title} ${'─'.repeat(Math.max(0, 46 - title.length))}`);
}

function pass(message) {
  console.info(`  PASS  ${message}`);
}

function fail(message, diagnosis, fixes) {
  state.failures += 1;
  console.error(`  FAIL  ${message}`);
  console.error(`        why: ${diagnosis}`);
  for (const fix of fixes) {
    console.error(`        fix: ${fix}`);
  }
}

function detail(message) {
  console.info(`        ${message}`);
}

/* ------------------------------------------------------------------- fetch */

async function request(url, options = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => {
    controller.abort();
  }, REQUEST_TIMEOUT_MS);

  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
      redirect: 'follow',
    });
    const text = await response.text();
    return { ok: response.ok, status: response.status, text, headers: response.headers };
  } catch (error) {
    return {
      ok: false,
      status: 0,
      text: '',
      error: error instanceof Error ? error.message : String(error),
    };
  } finally {
    clearTimeout(timeout);
  }
}

function parseJson(text) {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

/* ------------------------------------------------------------------- steps */

async function step1() {
  heading(1, 'Orthanc is running');

  const response = await request(`${ORTHANC_URL}/system`);

  if (!response.ok) {
    fail(
      `GET ${ORTHANC_URL}/system -> ${response.status || 'no response'}${response.error ? ` (${response.error})` : ''}`,
      'Orthanc is not listening on that address.',
      [
        'docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d',
        'docker compose ps            # is the orthanc container running?',
        'docker compose logs orthanc  # did it fail to parse its configuration?',
        'The dev overlay is what publishes port 8042; the base stack does not.',
      ],
    );
    return false;
  }

  const system = parseJson(response.text) ?? {};
  pass(`GET ${ORTHANC_URL}/system -> 200`);
  detail(
    `Orthanc ${system.Version ?? 'unknown version'}, database ${system.DatabaseVersion ?? '?'}`,
  );
  detail(`name: ${system.Name ?? 'unset'}`);
  return true;
}

async function step2() {
  heading(2, 'Orthanc contains studies');

  const response = await request(`${ORTHANC_URL}/studies?expand`);

  if (!response.ok) {
    fail(
      `GET ${ORTHANC_URL}/studies?expand -> ${response.status || 'no response'}`,
      response.status === 401
        ? 'Orthanc requires authentication, so the dev overlay is not applied.'
        : 'Orthanc answered /system but not /studies.',
      [
        'Restart with BOTH compose files so services/orthanc/zz-dev.json is mounted.',
        'docker compose logs orthanc',
      ],
    );
    return false;
  }

  const studies = parseJson(response.text);

  if (!Array.isArray(studies) || studies.length === 0) {
    fail(
      `GET ${ORTHANC_URL}/studies?expand -> 200 with 0 studies`,
      'Orthanc is running but empty. Nothing can be displayed until DICOM is imported.',
      [
        'Put DICOM under services/orthanc/seed/ then run: pnpm seed:orthanc',
        'Or upload one file directly:',
        `  curl -X POST ${ORTHANC_URL}/instances --data-binary @your-file.dcm`,
      ],
    );
    return false;
  }

  pass(`GET ${ORTHANC_URL}/studies?expand -> 200, ${studies.length} study(ies)`);

  for (const study of studies) {
    const tags = study.MainDicomTags ?? {};
    const patient = study.PatientMainDicomTags ?? {};
    detail(
      `${tags.StudyInstanceUID ?? '(no UID)'}  ${tags.StudyDate ?? '????????'}  ` +
        `${patient.PatientID ?? '(no MRN)'}  ${tags.StudyDescription || '(no description)'}`,
    );
  }

  return true;
}

async function step3() {
  heading(3, 'OHIF is served and can reach DICOMweb');

  const app = await request(`${VIEWER_URL}/`);

  if (!app.ok) {
    fail(
      `GET ${VIEWER_URL}/ -> ${app.status || 'no response'}${app.error ? ` (${app.error})` : ''}`,
      'The OHIF container is not serving.',
      [
        'docker compose ps',
        'docker compose logs ohif',
        'A bad nginx config mount makes the container exit immediately.',
      ],
    );
    return false;
  }
  pass(`GET ${VIEWER_URL}/ -> 200, ${app.text.length} bytes`);

  const config = await request(`${VIEWER_URL}/app-config.js`);
  if (config.ok && config.text.includes('dicomweb')) {
    pass(`GET ${VIEWER_URL}/app-config.js -> 200, CloudFilm data source present`);
  } else {
    fail(
      `GET ${VIEWER_URL}/app-config.js -> ${config.status}`,
      'The mounted app-config.js is not being served, so OHIF is using its own defaults and will not find Orthanc.',
      [
        'Confirm the image serves from /usr/share/nginx/html:',
        '  docker compose exec ohif ls -l /usr/share/nginx/html/app-config.js',
        'If the path differs, correct the volume mount in docker-compose.yml.',
      ],
    );
    return false;
  }

  // QIDO-RS through the viewer's own proxy: this is the exact path the browser
  // uses, so it is the only meaningful test of the proxy.
  const qido = await request(`${VIEWER_URL}/dicom-web/studies`, {
    headers: { Accept: 'application/dicom+json' },
  });

  if (!qido.ok) {
    fail(
      `GET ${VIEWER_URL}/dicom-web/studies -> ${qido.status || 'no response'}`,
      qido.status === 401
        ? 'Orthanc demanded credentials the proxy does not send.'
        : qido.status === 404
          ? 'The /dicom-web location is not proxying, or the DICOMweb plugin is not loaded in Orthanc.'
          : 'The viewer cannot reach Orthanc.',
      [
        'docker compose exec ohif cat /etc/nginx/conf.d/default.conf   # is our config mounted?',
        `curl -i ${ORTHANC_URL}/dicom-web/studies                       # is the plugin loaded at all?`,
        'docker compose logs orthanc | grep -i dicomweb',
      ],
    );
    return false;
  }

  const found = parseJson(qido.text);
  if (!Array.isArray(found) || found.length === 0) {
    fail(
      `GET ${VIEWER_URL}/dicom-web/studies -> 200 with 0 studies`,
      'The proxy works but DICOMweb reports no studies, although Orthanc has some.',
      [
        'docker compose logs orthanc | grep -i dicomweb',
        'Confirm the DicomWeb plugin is enabled in services/orthanc/orthanc.json.',
      ],
    );
    return false;
  }

  pass(`GET ${VIEWER_URL}/dicom-web/studies -> 200, ${found.length} study(ies) via QIDO-RS`);
  for (const study of found) {
    detail(`0020000D = ${study['0020000D']?.Value?.[0] ?? '(missing)'}`);
  }

  return true;
}

async function step4() {
  heading(4, 'The portal is serving and points at this viewer');

  const home = await request(`${PORTAL_URL}/api/health`);
  if (!home.ok) {
    fail(
      `GET ${PORTAL_URL}/api/health -> ${home.status || 'no response'}`,
      'The portal is not running.',
      ['pnpm dev', 'or: docker compose ps portal && docker compose logs portal'],
    );
    return false;
  }
  pass(`GET ${PORTAL_URL}/api/health -> 200`);

  detail(`the portal must be configured with CLOUDFILM_VIEWER_URL=${VIEWER_URL}`);
  detail('an address the BROWSER can reach - a container name will not work');
  detail('the iframe itself can only be confirmed visually; see Step 4 in docs/local-imaging.md');

  return true;
}

async function step5() {
  heading(5, 'Portal study UIDs exist in Orthanc');

  let fixtures;
  try {
    fixtures = JSON.parse(await readFile(FIXTURE_FILE, 'utf8'));
  } catch (error) {
    fail(`cannot read ${FIXTURE_FILE}`, error instanceof Error ? error.message : String(error), [
      'git checkout packages/shared/mock/seeded-studies.json',
    ]);
    return false;
  }

  const portalUids = (fixtures.records ?? []).flatMap((record) =>
    (record.studies ?? []).map((study) => study.studyInstanceUid),
  );

  if (portalUids.length === 0) {
    fail(
      'seeded-studies.json contains no studies',
      'The portal is serving built-in placeholder fixtures. Their UIDs are synthetic, so the viewer cannot resolve them.',
      ['pnpm seed:orthanc', 'then restart the portal so it re-reads the file'],
    );
    return false;
  }

  const response = await request(`${ORTHANC_URL}/studies?expand`);
  const orthancUids = new Set(
    (parseJson(response.text) ?? []).map((study) => study.MainDicomTags?.StudyInstanceUID),
  );

  const missing = portalUids.filter((uid) => !orthancUids.has(uid));

  if (missing.length > 0) {
    fail(
      `${missing.length} of ${portalUids.length} portal UID(s) are not in Orthanc`,
      'The fixtures are stale relative to Orthanc, so Open Study would hand the viewer an unresolvable UID.',
      ['pnpm seed:orthanc   # regenerate from what Orthanc holds now', 'then restart the portal'],
    );
    for (const uid of missing) {
      detail(`missing: ${uid}`);
    }
    return false;
  }

  pass(`all ${portalUids.length} portal UID(s) exist in Orthanc`);
  detail(
    `fixtures generated ${fixtures.generatedAt ?? 'unknown'} from ${fixtures.source ?? 'unknown'}`,
  );

  console.info('\n  Open these to finish verification by eye:');
  for (const uid of portalUids) {
    detail(`workspace: ${PORTAL_URL}/workspace/${encodeURIComponent(uid)}`);
    detail(`viewer:    ${VIEWER_URL}/viewer?StudyInstanceUIDs=${encodeURIComponent(uid)}`);
  }

  return true;
}

/* -------------------------------------------------------------------- main */

const STEPS = [step1, step2, step3, step4, step5];

const requested = process.argv.includes('--step')
  ? Number(process.argv[process.argv.indexOf('--step') + 1])
  : undefined;

if (
  requested !== undefined &&
  (!Number.isInteger(requested) || requested < 1 || requested > STEPS.length)
) {
  console.error(`--step must be between 1 and ${STEPS.length}`);
  process.exit(2);
}

const toRun = requested === undefined ? STEPS : [STEPS[requested - 1]];

for (const step of toRun) {
  const ok = await step();
  if (!ok) {
    console.error('\nStopped. A later step cannot be trusted while this one fails.');
    process.exit(1);
  }
}

console.info(`\nAll checked steps passed. Failures: ${state.failures}`);
