#!/usr/bin/env node
/**
 * Step-gated verification of the local imaging stack.
 *
 * Each step proves one link in the chain that ends with a real DICOM study on
 * screen, and - once shared or analysed - one link in each of the paths that lead
 * away from it. Steps run in order and stop at the first failure, because a later
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
 *   API_URL       default http://localhost:8080
 *
 *   SEED_ADMIN_EMAIL, SEED_ADMIN_PASSWORD    required by the sharing steps (6-8);
 *   CLOUDFILM_ADMIN_EMAIL / _PASSWORD override them. These are the credentials an
 *   administrator would sign in with; nothing is hard-coded and nothing is stored.
 *
 * Orthanc is not published by either compose file. Steps 1, 2 and 5 talk to it
 * directly and therefore need the deliberate, temporary port override described in
 * docs/local-imaging.md. Steps 3, 4 and 6-8 do not - they use the paths a browser
 * uses, which is the point.
 */

import { readFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const FIXTURE_FILE = join(REPO_ROOT, 'packages', 'shared', 'mock', 'seeded-studies.json');

const ORTHANC_URL = trimUrl(process.env['ORTHANC_URL'] ?? 'http://localhost:8042');
const VIEWER_URL = trimUrl(process.env['VIEWER_URL'] ?? 'http://localhost:8081');
const PORTAL_URL = trimUrl(process.env['PORTAL_URL'] ?? 'http://localhost:3000');
const API_URL = trimUrl(process.env['API_URL'] ?? 'http://localhost:8080');

/** The cookie the viewer's proxy checks. Must match the portal and the API. */
const VIEWER_COOKIE = 'cloudfilm_viewer';

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

      // Following by default, but overridable: the share landing route answers
      // with a redirect and a cookie, and following it would discard both.
      redirect: options.redirect ?? 'follow',
    });
    const text = await response.text();
    return {
      ok: response.ok,
      status: response.status,
      text,
      headers: response.headers,
      location: response.headers.get('location') ?? undefined,
      setCookie: response.headers.getSetCookie?.() ?? [],
    };
  } catch (error) {
    return {
      ok: false,
      status: 0,
      text: '',
      setCookie: [],
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
  heading(3, 'OHIF is served and its DICOMweb path is gated');

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

  if (config.text.includes('showStudyList: false')) {
    pass('app-config.js disables the study list');
  } else {
    fail(
      'app-config.js does not set showStudyList: false',
      "OHIF's study list is an unfiltered query for every study in the archive. The gate refuses that, so the list would greet every visitor with an error - and a viewer that offers to browse the archive is the wrong shape for a shared link.",
      ['Check services/ohif/app-config.js, then: docker compose restart ohif'],
    );
    return false;
  }

  // The DICOMweb path with no viewer session. This used to answer 200 with the
  // whole archive; it must now answer an empty 404, because nginx asks the API
  // whether the caller may have it and the API says nothing at all.
  //
  // The empty body is what distinguishes a working gate from a missing proxy: a
  // location that is not there falls through to the SPA and returns index.html.
  const unfiltered = await request(`${VIEWER_URL}/dicom-web/studies`, {
    headers: { Accept: 'application/dicom+json' },
  });

  if (unfiltered.status !== 404 || unfiltered.text.length > 0) {
    fail(
      `GET ${VIEWER_URL}/dicom-web/studies (no session) -> ${unfiltered.status || 'no response'}, ${unfiltered.text.length} bytes`,
      unfiltered.status === 200 && unfiltered.text.includes('<')
        ? 'The /dicom-web location is not active: nginx served the single-page app instead. The gate is not in the request path at all.'
        : unfiltered.status === 200
          ? 'The archive answered an unfiltered query from a caller with no viewer session. The gate is not enforcing.'
          : unfiltered.status === 500
            ? 'nginx could not complete the authorisation subrequest - usually the API being unreachable, or auth_request not compiled into this nginx.'
            : 'Unexpected answer from the gate.',
      [
        'docker compose exec ohif nginx -V 2>&1 | grep -c http_auth_request_module   # 1 = present',
        'docker compose exec ohif cat /etc/nginx/conf.d/default.conf | grep -A3 auth_request',
        'docker compose logs api | tail -30',
      ],
    );
    return false;
  }

  pass(`GET ${VIEWER_URL}/dicom-web/studies (no session) -> 404, empty - the gate is closed`);
  detail('that DICOMweb actually reaches Orthanc is proved in Step 6, with a session');

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

/* ---------------------------------------------------------------- sharing */

/**
 * The administrator credentials the sharing steps sign in with.
 *
 * Read from the environment, never from the repository. A missing credential
 * fails the run: silently skipping the checks that prove who can reach a
 * patient's imaging would turn a green result into a false one.
 */
function adminCredentials() {
  const email = process.env['CLOUDFILM_ADMIN_EMAIL'] ?? process.env['SEED_ADMIN_EMAIL'] ?? '';
  const password =
    process.env['CLOUDFILM_ADMIN_PASSWORD'] ?? process.env['SEED_ADMIN_PASSWORD'] ?? '';

  if (email.length === 0 || password.length === 0) {
    fail(
      'no administrator credentials in the environment',
      'Steps 6-8 sign in to issue and withdraw share links. Without a credential they cannot run, and a run that skipped them would report a security path as verified when nothing checked it.',
      [
        'export SEED_ADMIN_EMAIL=... SEED_ADMIN_PASSWORD=...   # the same pair in your .env',
        'or CLOUDFILM_ADMIN_EMAIL / CLOUDFILM_ADMIN_PASSWORD to use a different account',
        'The account must hold the Administrator role.',
      ],
    );
    return undefined;
  }

  return { email, password };
}

/** Signs in and returns a bearer token, or undefined after reporting why not. */
async function signIn(credentials) {
  const response = await request(`${API_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email: credentials.email, password: credentials.password }),
  });

  const result = parseJson(response.text);

  if (!response.ok || typeof result?.accessToken !== 'string') {
    fail(
      `POST ${API_URL}/api/v1/auth/login -> ${response.status || 'no response'}`,
      response.status === 401
        ? 'The API rejected those credentials.'
        : 'The API did not answer the sign-in.',
      [
        `Is the API up? curl -i ${API_URL}/health/ready`,
        'Check SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD against the running deployment.',
      ],
    );
    return undefined;
  }

  if (!(result.roles ?? []).includes('Administrator')) {
    fail(
      'that account is not an Administrator',
      'Issuing and withdrawing share links is administrator-only, by design.',
      ['Sign in as an administrator, or grant the role to this account.'],
    );
    return undefined;
  }

  pass(`signed in as ${credentials.email} with roles [${(result.roles ?? []).join(', ')}]`);

  return result.accessToken;
}

/** Issues a share link. Returns { id, token } - the token is never printed. */
async function createShareLink(accessToken, studyInstanceUid, permission) {
  const response = await request(`${API_URL}/api/v1/share-links`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({
      studyInstanceUid,

      // One hour. Well inside the seven-day cap, and short enough that a link
      // left behind by a failed run stops working the same morning.
      expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
      permission,
    }),
  });

  const created = parseJson(response.text);

  if (response.status !== 201 || typeof created?.token !== 'string') {
    fail(
      `POST ${API_URL}/api/v1/share-links (${permission}) -> ${response.status || 'no response'}`,
      permission === 'View' && response.status === 400
        ? 'The API refused a View link. This is the S7.5 change: an API still on the old behaviour issues Download links only.'
        : 'The link was not issued.',
      ['docker compose logs api | tail -30'],
    );
    return undefined;
  }

  // Deliberately not printed. It is a bearer credential for a patient's study,
  // and this output is meant to be pasted into issues and chat logs.
  pass(
    `POST /api/v1/share-links (${permission}) -> 201, token received (${created.token.length} characters, not shown)`,
  );
  detail(`link id ${created.id}, permission ${created.permission}`);

  if (created.permission !== permission) {
    fail(
      `the link came back as ${created.permission}`,
      'A link stored with a wider permission than was asked for would hand its recipient more than the administrator agreed to.',
      ['Check ShareLinkEndpoints.cs: the request permission must reach store.CreateAsync.'],
    );
    return undefined;
  }

  return { id: created.id, token: created.token };
}

/** Withdraws a share link. */
async function revokeShareLink(accessToken, shareLinkId) {
  const response = await request(`${API_URL}/api/v1/share-links/${shareLinkId}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (response.status !== 204) {
    fail(
      `DELETE ${API_URL}/api/v1/share-links/${shareLinkId} -> ${response.status || 'no response'}`,
      'The link could not be withdrawn, so the revocation check below would prove nothing.',
      ['docker compose logs api | tail -30'],
    );
    return false;
  }

  pass('DELETE /api/v1/share-links/{id} -> 204, link withdrawn');
  return true;
}

/**
 * Walks the anonymous landing route the way a recipient's browser does.
 *
 * The portal, not the API, is what a recipient opens: it redeems the token
 * server-side, sets the viewer cookie and redirects into OHIF. Following the
 * redirect would discard both, so this stops at it.
 */
async function redeemThroughPortal(token) {
  const response = await request(`${PORTAL_URL}/share/${token}`, { redirect: 'manual' });

  if (response.status < 300 || response.status >= 400) {
    fail(
      `GET ${PORTAL_URL}/share/{token} -> ${response.status || 'no response'}`,
      response.status === 404
        ? 'The portal refused the token. Unknown, expired, withdrawn and not-permitted all look like this, deliberately.'
        : 'The share landing route did not redirect into the viewer.',
      ['docker compose logs portal | tail -30', 'docker compose logs api | tail -30'],
    );
    return undefined;
  }

  const cookie = response.setCookie
    .map((value) => value.split(';')[0])
    .find((value) => value.startsWith(`${VIEWER_COOKIE}=`));

  if (cookie === undefined) {
    fail(
      `GET ${PORTAL_URL}/share/{token} -> ${response.status} with no ${VIEWER_COOKIE} cookie`,
      'Without the cookie the viewer has nothing to present to the gate, and every image would be refused.',
      ['Check apps/portal/src/app/share/[token]/route.ts and lib/viewer-session.ts'],
    );
    return undefined;
  }

  const attributes =
    response.setCookie.find((value) => value.startsWith(`${VIEWER_COOKIE}=`)) ?? '';

  pass(`GET ${PORTAL_URL}/share/{token} -> ${response.status} to the viewer, ${VIEWER_COOKIE} set`);
  detail(
    `HttpOnly: ${/httponly/i.test(attributes)}, SameSite: ${/samesite=(\w+)/i.exec(attributes)?.[1] ?? '(none)'}`,
  );
  detail(`redirect target: ${response.location ?? '(none)'}`);

  return cookie;
}

/** One DICOMweb request through the viewer's proxy, carrying a session cookie. */
function dicomWeb(path, cookie) {
  return request(`${VIEWER_URL}${path}`, {
    headers: {
      Accept: 'application/dicom+json',
      ...(cookie === undefined ? {} : { Cookie: cookie }),
    },
  });
}

/** The first study the portal fixtures name, which Step 5 has proved Orthanc holds. */
async function firstStudyUid() {
  try {
    const fixtures = JSON.parse(await readFile(FIXTURE_FILE, 'utf8'));
    const uid = (fixtures.records ?? []).flatMap((record) =>
      (record.studies ?? []).map((study) => study.studyInstanceUid),
    )[0];

    if (typeof uid === 'string' && uid.length > 0) {
      return uid;
    }
  } catch {
    // Falls through to the failure below, which says what to do about it.
  }

  fail(
    'no seeded study to share',
    'The sharing steps need a study that actually exists, so that a success means the archive answered rather than that nothing was asked.',
    ['pnpm seed:orthanc', 'then re-run from Step 1'],
  );

  return undefined;
}

async function step6() {
  heading(6, 'A View share link opens one study and nothing else');

  const credentials = adminCredentials();
  if (credentials === undefined) {
    return false;
  }

  const studyInstanceUid = await firstStudyUid();
  if (studyInstanceUid === undefined) {
    return false;
  }

  const accessToken = await signIn(credentials);
  if (accessToken === undefined) {
    return false;
  }

  const link = await createShareLink(accessToken, studyInstanceUid, 'View');
  if (link === undefined) {
    return false;
  }

  const cookie = await redeemThroughPortal(link.token);
  if (cookie === undefined) {
    return false;
  }

  // 1. The permitted study, through the gate. This is also the first proof in
  //    the run that DICOMweb reaches Orthanc at all.
  const permitted = await dicomWeb(
    `/dicom-web/studies?StudyInstanceUID=${encodeURIComponent(studyInstanceUid)}`,
    cookie,
  );
  const permittedStudies = parseJson(permitted.text);

  if (
    permitted.status !== 200 ||
    !Array.isArray(permittedStudies) ||
    permittedStudies.length === 0
  ) {
    fail(
      `GET /dicom-web/studies?StudyInstanceUID=... (with session) -> ${permitted.status || 'no response'}`,
      permitted.status === 404
        ? 'The gate refused a request for the very study the link names. The session, the cookie or the study binding is wrong.'
        : 'The archive did not answer for the permitted study.',
      [
        'docker compose logs api | grep -i authorize | tail -20',
        'docker compose logs ohif | tail -20',
      ],
    );
    return false;
  }
  pass(`filtered QIDO for the shared study -> 200, ${permittedStudies.length} result(s)`);

  // 2. The archive's whole index, with a valid session. Still refused.
  const unfiltered = await dicomWeb('/dicom-web/studies', cookie);
  if (unfiltered.status !== 404) {
    fail(
      `GET /dicom-web/studies (with session) -> ${unfiltered.status}`,
      'A session admits one study. An unfiltered query returns every study of every patient, and a session holder must not be able to make one.',
      [
        'Check the gate: an absent StudyInstanceUID filter must be refused, not treated as unrestricted.',
      ],
    );
    return false;
  }
  pass('unfiltered QIDO with a valid session -> 404');

  // 3. A different study. The identifier is not a secret, so this is the
  //    request a curious recipient would actually try.
  const other = `${studyInstanceUid}.999`;
  const foreign = await dicomWeb(
    `/dicom-web/studies/${encodeURIComponent(other)}/metadata`,
    cookie,
  );
  if (foreign.status !== 404) {
    fail(
      `GET /dicom-web/studies/{another UID}/metadata -> ${foreign.status}`,
      'The session is bound to one study and must admit nothing else.',
      ['Check Admits() in ShareViewEndpoints.cs'],
    );
    return false;
  }
  pass('a different Study Instance UID -> 404');

  // 4. The download route, with the same link. This is what View means.
  const download = await request(`${API_URL}/api/v1/share/${link.token}/download`);
  if (download.status !== 404 || download.text.length > 0) {
    fail(
      `GET ${API_URL}/api/v1/share/{token}/download (View link) -> ${download.status}, ${download.text.length} bytes`,
      'A view link handed over the study file. View must admit the viewer and refuse the download, with the same empty 404 an unknown token gets.',
      ['Check ShareAccessEndpoints.cs: GrantPermissions.Satisfies(target.Permission, Download)'],
    );
    return false;
  }
  pass('the same View link on the download route -> 404, empty');

  // 5. Withdraw it, then ask again with the session that was already working.
  if (!(await revokeShareLink(accessToken, link.id))) {
    return false;
  }

  const afterRevocation = await dicomWeb(
    `/dicom-web/studies?StudyInstanceUID=${encodeURIComponent(studyInstanceUid)}`,
    cookie,
  );

  if (afterRevocation.status !== 404) {
    fail(
      `the same filtered QIDO after revocation -> ${afterRevocation.status}`,
      'Withdrawing a link must stop the next image, not the next session. The gate re-checks the link on every request precisely for this.',
      ['Check IShareLinkStatus.IsActiveAsync and its use in the gate.'],
    );
    return false;
  }
  pass('the same request after revocation -> 404, within one request');

  return true;
}

async function step7() {
  heading(7, 'Authenticated viewing still works, through the same gate');

  const credentials = adminCredentials();
  if (credentials === undefined) {
    return false;
  }

  const studyInstanceUid = await firstStudyUid();
  if (studyInstanceUid === undefined) {
    return false;
  }

  const accessToken = await signIn(credentials);
  if (accessToken === undefined) {
    return false;
  }

  const authorization = { Authorization: `Bearer ${accessToken}`, Accept: 'application/json' };

  const study = await request(`${API_URL}/api/v1/studies/${encodeURIComponent(studyInstanceUid)}`, {
    headers: authorization,
  });

  if (!study.ok) {
    fail(
      `GET ${API_URL}/api/v1/studies/{uid} -> ${study.status || 'no response'}`,
      'The signed-in imaging path is broken, independently of anything to do with sharing.',
      ['docker compose logs api | tail -30', 'docker compose logs retrieve | tail -30'],
    );
    return false;
  }
  pass(`GET /api/v1/studies/{uid} -> 200, the authenticated catalogue still answers`);

  const session = await request(
    `${API_URL}/api/v1/imaging/studies/${encodeURIComponent(studyInstanceUid)}/viewer-session`,
    { method: 'POST', headers: authorization },
  );

  const minted = parseJson(session.text);

  if (!session.ok || typeof minted?.sessionId !== 'string') {
    fail(
      `POST /api/v1/imaging/studies/{uid}/viewer-session -> ${session.status || 'no response'}`,
      'A signed-in clinician cannot obtain a viewer session, so the workspace would show an empty viewer.',
      ['docker compose logs api | tail -30'],
    );
    return false;
  }
  pass(`POST /api/v1/imaging/studies/{uid}/viewer-session -> 200, permission ${minted.permission}`);
  detail(`expires ${minted.expiresAtUtc}`);

  const cookie = `${VIEWER_COOKIE}=${minted.sessionId}`;

  const permitted = await dicomWeb(
    `/dicom-web/studies?StudyInstanceUID=${encodeURIComponent(studyInstanceUid)}`,
    cookie,
  );

  if (permitted.status !== 200) {
    fail(
      `filtered QIDO with an authenticated session -> ${permitted.status}`,
      'The workspace mints exactly this session before it loads the iframe. If the gate refuses it, signed-in viewing is broken.',
      ['docker compose logs api | grep -i authorize | tail -20'],
    );
    return false;
  }
  pass('filtered QIDO with an authenticated session -> 200');

  const unfiltered = await dicomWeb('/dicom-web/studies', cookie);
  if (unfiltered.status !== 404) {
    fail(
      `unfiltered QIDO with an authenticated session -> ${unfiltered.status}`,
      'A clinician entitled to the whole catalogue is still admitted to one study at a time in the viewer. That is what keeps the cookie worth little if it leaks.',
      ['Check the gate: the rule is the same for both kinds of session.'],
    );
    return false;
  }
  pass('unfiltered QIDO with an authenticated session -> 404, same rule for both');

  return true;
}

async function step8() {
  heading(8, 'A Download share link still downloads');

  const credentials = adminCredentials();
  if (credentials === undefined) {
    return false;
  }

  const studyInstanceUid = await firstStudyUid();
  if (studyInstanceUid === undefined) {
    return false;
  }

  const accessToken = await signIn(credentials);
  if (accessToken === undefined) {
    return false;
  }

  const link = await createShareLink(accessToken, studyInstanceUid, 'Download');
  if (link === undefined) {
    return false;
  }

  // The body is a whole study. It is checked by its headers and then cancelled
  // unread: this is a verification script, not a client, and buffering a study
  // to prove it arrived would be the one part of the run that could exhaust a
  // developer's memory.
  let response;
  try {
    response = await fetch(`${API_URL}/api/v1/share/${link.token}/download`, {
      headers: { Accept: 'multipart/related' },
    });
  } catch (error) {
    fail(
      'GET /api/v1/share/{token}/download did not answer',
      error instanceof Error ? error.message : String(error),
      ['docker compose logs api | tail -30'],
    );
    return false;
  }

  const contentType = response.headers.get('content-type') ?? '(none)';
  const disposition = response.headers.get('content-disposition') ?? '(none)';

  await response.body?.cancel();

  if (response.status !== 200) {
    fail(
      `GET /api/v1/share/{token}/download (Download link) -> ${response.status}`,
      response.status === 404
        ? 'The download was refused. Either the study is not in the archive, or the link was not issued with Download.'
        : 'The download did not complete.',
      ['docker compose logs api | tail -30', 'docker compose logs retrieve | tail -30'],
    );
    return false;
  }

  pass(`GET /api/v1/share/{token}/download -> 200, ${contentType}`);

  if (disposition.includes(studyInstanceUid)) {
    fail(
      `Content-Disposition names the study: ${disposition}`,
      "A filename drawn from the study puts an identifier into the recipient's downloads folder and into every log that records a filename.",
      ['Check ShareAccessEndpoints.cs: the download name is a constant.'],
    );
    return false;
  }
  pass(`Content-Disposition carries no study identifier: ${disposition}`);

  return revokeShareLink(accessToken, link.id);
}

/* ------------------------------------------------------------- analysis ---*/

/**
 * The patient identity the seeded fixtures carry.
 *
 * Read so that the analysis response can be checked for it. The service is inside
 * the PHI boundary - DICOM carries a patient's name and record number in its own
 * tags - and the one guarantee that matters on this path is that none of it comes
 * back out. Asserting against the real values is the only version of that check
 * worth running.
 */
async function seededPatientIdentity() {
  try {
    const fixtures = JSON.parse(await readFile(FIXTURE_FILE, 'utf8'));
    const record = (fixtures.records ?? [])[0];

    return {
      fullName: record?.patient?.fullName ?? '',
      medicalRecordNumber: record?.patient?.medicalRecordNumber ?? '',
    };
  } catch {
    return { fullName: '', medicalRecordNumber: '' };
  }
}

async function step9() {
  heading(9, 'A study can be analysed, and the result carries no patient identity');

  const credentials = adminCredentials();
  if (credentials === undefined) {
    return false;
  }

  const studyInstanceUid = await firstStudyUid();
  if (studyInstanceUid === undefined) {
    return false;
  }

  const accessToken = await signIn(credentials);
  if (accessToken === undefined) {
    return false;
  }

  // Through the API, because that is the only way in. The analysis service
  // publishes no host port, so there is nothing here to curl directly - and a
  // check that bypassed the API would not be checking the path a clinician uses.
  const response = await request(
    `${API_URL}/api/v1/imaging/studies/${encodeURIComponent(studyInstanceUid)}/analysis`,
    {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}`, Accept: 'application/json' },
    },
  );

  if (response.status === 501) {
    fail(
      `POST /api/v1/imaging/studies/{uid}/analysis -> 501`,
      'The API has no analysis service configured, so it registered no analyser. The portal reports this as "not available in this deployment".',
      [
        'docker compose up -d --build analysis',
        'docker compose up -d --force-recreate api   # `restart` does not re-read the environment',
        'docker compose exec api printenv Downstream__AnalysisServiceUrl   # expect http://analysis:8000',
      ],
    );
    return false;
  }

  if (!response.ok) {
    fail(
      `POST /api/v1/imaging/studies/{uid}/analysis -> ${response.status || 'no response'}`,
      response.status === 404
        ? 'The study could not be analysed: the archive does not hold it, it holds no instances, or this account may not download it.'
        : 'The analysis did not complete. The API answers 500 for a downstream failure and never repeats what the service said.',
      [
        'docker compose logs analysis | tail -30',
        'docker compose logs api | tail -30',
        'docker compose ps analysis   # healthy?',
      ],
    );
    return false;
  }

  const analysis = parseJson(response.text);

  if (analysis === undefined) {
    fail(
      'the analysis response is not JSON',
      'The API returned something the portal could not render.',
      ['docker compose logs api | tail -30'],
    );
    return false;
  }

  // The contract, exactly. A field that appeared here and nowhere in
  // StudyAnalysis would be one nothing has agreed to carry.
  const expected = ['studyInstanceUid', 'modelName', 'modelVersion', 'completedAtUtc', 'findings'];
  const actual = Object.keys(analysis).sort();

  if (actual.join(',') !== [...expected].sort().join(',')) {
    fail(
      `the response carries ${actual.join(', ')}`,
      'The response does not match the StudyAnalysis contract the portal and the API agree on.',
      [
        'Compare apps/analysis/app/models.py with CloudFilm.Application/Analysis/AnalysisContracts.cs',
      ],
    );
    return false;
  }
  pass('the response carries exactly the StudyAnalysis contract');

  if (analysis.studyInstanceUid !== studyInstanceUid) {
    fail(
      'the result is filed under a different study',
      'A finding attributed to the wrong study is a finding attributed to the wrong patient.',
      ['Check StudyAnalyser.cs: the authorised UID is what the result must carry.'],
    );
    return false;
  }

  pass(`analysed by ${analysis.modelName} ${analysis.modelVersion}`);
  detail(`completed ${analysis.completedAtUtc}`);

  const findings = Array.isArray(analysis.findings) ? analysis.findings : [];
  const outOfRange = findings.filter((f) => !(f.score >= 0 && f.score <= 1));

  if (outOfRange.length > 0) {
    fail(
      `${outOfRange.length} finding(s) score outside [0, 1]`,
      'The contract constrains a score to a proportion. A value outside it reaches a clinician as a number nobody can interpret.',
      ['Check the analyser: a measurement is normalised before it becomes a score.'],
    );
    return false;
  }

  pass(`${findings.length} observation(s), every score within [0, 1]`);
  for (const finding of findings) {
    detail(`${finding.label}  ${finding.score}`);
  }

  // The PHI boundary, checked against the real values rather than a pattern.
  const patient = await seededPatientIdentity();
  const leaked = [patient.fullName, patient.medicalRecordNumber]
    .filter((value) => value.length > 0)
    .filter((value) => response.text.includes(value));

  if (leaked.length > 0) {
    fail(
      'the analysis response contains patient identity',
      'The service receives DICOM, which carries the patient name and record number in its own tags. None of it may come back out.',
      ['Check apps/analysis/app/analyzer.py: only the agreed fields are returned.'],
    );
    return false;
  }

  pass('no patient name or record number appears in the result');
  detail('a successful run also writes one ANALYSE_STUDY audit row');

  return true;
}

/* -------------------------------------------------------------------- main */

const STEPS = [step1, step2, step3, step4, step5, step6, step7, step8, step9];

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
