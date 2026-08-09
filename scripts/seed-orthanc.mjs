#!/usr/bin/env node
/**
 * Loads sample DICOM into the local development Orthanc, then records what
 * landed there as portal fixtures.
 *
 * Why the second half matters: the portal opens a study by handing OHIF a Study
 * Instance UID. Those UIDs are minted by whoever produced the DICOM, so a
 * fixture can never guess them. This script reads back what Orthanc actually
 * holds and writes it to packages/shared/mock/seeded-studies.json, which the
 * mock layer prefers over its built-in fixtures. That is the whole reason
 * "Open Study" can display a real image.
 *
 * Local development only. It talks to Orthanc directly, which nothing in the
 * application is permitted to do.
 *
 *   node scripts/seed-orthanc.mjs
 *
 * Environment:
 *   ORTHANC_URL                  default http://localhost:8042
 *   CLOUDFILM_SAMPLE_DICOM_URL   zip of DICOM to fall back on when no local
 *                                files are present
 */

import { createWriteStream } from 'node:fs';
import { mkdir, readdir, readFile, stat, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { tmpdir } from 'node:os';

const execFileAsync = promisify(execFile);

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SEED_DIR = join(REPO_ROOT, 'services', 'orthanc', 'seed');
const FIXTURE_FILE = join(REPO_ROOT, 'packages', 'shared', 'mock', 'seeded-studies.json');

const ORTHANC_URL = (process.env['ORTHANC_URL'] ?? 'http://localhost:8042').replace(/\/+$/, '');
const ORTHANC_USERNAME = process.env['ORTHANC_USERNAME'] ?? 'cloudfilm';
const ORTHANC_PASSWORD = process.env['ORTHANC_PASSWORD'] ?? '';
const SAMPLE_URL = process.env['CLOUDFILM_SAMPLE_DICOM_URL'] ?? '';

/**
 * Builds request headers, adding Basic authentication when a password is set.
 *
 * Orthanc rejects anonymous requests with 401 whenever AuthenticationEnabled is
 * on. Left off when no password is configured, so a development Orthanc with
 * authentication disabled still works unchanged.
 */
function orthancHeaders(extra = {}) {
  if (ORTHANC_PASSWORD.length === 0) {
    return extra;
  }

  const token = Buffer.from(`${ORTHANC_USERNAME}:${ORTHANC_PASSWORD}`).toString('base64');

  return {
    Authorization: `Basic ${token}`,
    ...extra,
  };
}

/** Files that are certainly not DICOM, so we do not waste a POST on them. */
const IGNORED_NAMES = new Set(['.gitkeep', '.gitignore', '.DS_Store', 'README.md']);

async function main() {
  await assertOrthancReachable();

  let files = await collectSeedFiles();

  if (files.length === 0) {
    files = await downloadSample();
  }

  if (files.length === 0) {
    explainMissingData();
    process.exitCode = 1;
    return;
  }

  console.info(`Uploading ${files.length} file(s) to ${ORTHANC_URL} …`);
  const { accepted, rejected } = await uploadAll(files);
  console.info(`  accepted ${accepted}, not DICOM or rejected ${rejected}`);

  if (accepted === 0) {
    console.error('\nOrthanc accepted nothing. The files above are probably not DICOM.');
    process.exitCode = 1;
    return;
  }

  const records = await readBackRecords();
  await writeFixtures(records);

  const studyCount = records.reduce((total, record) => total + record.studies.length, 0);
  console.info(`\nWrote ${records.length} patient(s) and ${studyCount} study(ies) to`);
  console.info(`  ${FIXTURE_FILE}`);
  console.info('\nRestart `pnpm dev` so the portal picks the fixtures up, then search for:');
  for (const record of records) {
    console.info(`  ${record.patient.medicalRecordNumber}  (${record.patient.fullName})`);
  }
}

async function assertOrthancReachable() {
  try {
    const response = await fetch(`${ORTHANC_URL}/system`, {
      headers: orthancHeaders(),
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const system = await response.json();
    console.info(`Orthanc ${system.Version ?? 'unknown'} at ${ORTHANC_URL}`);
  } catch (error) {
    console.error(`Cannot reach Orthanc at ${ORTHANC_URL}.`);
    console.error(`  ${error instanceof Error ? error.message : String(error)}`);
    console.error('\nStart the development stack first:');
    console.error('  docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d');
    process.exit(1);
  }
}

async function collectSeedFiles() {
  await mkdir(SEED_DIR, { recursive: true });
  return walk(SEED_DIR);
}

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = join(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await walk(path)));
    } else if (!IGNORED_NAMES.has(entry.name)) {
      files.push(path);
    }
  }

  return files;
}

async function downloadSample() {
  if (SAMPLE_URL.length === 0) {
    return [];
  }

  console.info(`No local files in services/orthanc/seed, downloading ${SAMPLE_URL} …`);

  const workingDirectory = join(tmpdir(), `cloudfilm-sample-${Date.now()}`);
  await mkdir(workingDirectory, { recursive: true });
  const archive = join(workingDirectory, 'sample.zip');

  try {
    const response = await fetch(SAMPLE_URL, { redirect: 'follow' });
    if (!response.ok || response.body === null) {
      throw new Error(`HTTP ${response.status}`);
    }
    await pipeline(Readable.fromWeb(response.body), createWriteStream(archive));

    // `unzip` rather than a dependency: this is a development-only script and
    // the repository has no runtime need for an archive library.
    await execFileAsync('unzip', ['-qo', archive, '-d', workingDirectory]);
    return (await walk(workingDirectory)).filter((path) => path !== archive);
  } catch (error) {
    console.warn(`  download failed: ${error instanceof Error ? error.message : String(error)}`);
    return [];
  }
}

function explainMissingData() {
  console.error('\nNo DICOM to load.');
  console.error('\nPut a CT or CBCT study here and run this again:');
  console.error(`  ${SEED_DIR}`);
  console.error('\nAny .dcm files work, including a whole directory tree. Public sample');
  console.error('studies are published by the Orthanc project, the OHIF project, and');
  console.error('The Cancer Imaging Archive. Alternatively point the script at a zip:');
  console.error('  CLOUDFILM_SAMPLE_DICOM_URL=https://… node scripts/seed-orthanc.mjs');
}

async function uploadAll(files) {
  let accepted = 0;
  let rejected = 0;

  for (const file of files) {
    const info = await stat(file);
    if (!info.isFile() || info.size === 0) {
      continue;
    }

    const body = await readFile(file);
    const response = await fetch(`${ORTHANC_URL}/instances`, {
      method: 'POST',
      headers: orthancHeaders({
        'Content-Type': 'application/dicom',
      }),
      body,
    });

    if (response.ok) {
      accepted += 1;
    } else {
      rejected += 1;
    }
  }

  return { accepted, rejected };
}

async function readBackRecords() {
  const studies = await getJson('/studies?expand');
  const byPatient = new Map();

  for (const study of studies) {
    const tags = study.MainDicomTags ?? {};
    const patientTags = study.PatientMainDicomTags ?? {};

    const series = await getJson(`/studies/${study.ID}/series?expand`);
    const instanceCount = series.reduce(
      (total, entry) => total + (Array.isArray(entry.Instances) ? entry.Instances.length : 0),
      0,
    );
    const modality = series[0]?.MainDicomTags?.Modality ?? 'OT';

    const medicalRecordNumber = String(patientTags.PatientID ?? '').trim() || study.ID;
    const existing = byPatient.get(medicalRecordNumber);

    const mapped = {
      studyInstanceUid: String(tags.StudyInstanceUID ?? '').trim(),
      modality: String(modality).trim().toUpperCase(),
      description: String(tags.StudyDescription ?? '').trim() || 'Untitled study',
      performedOn: toIsoDate(tags.StudyDate),
      status: 'Completed',
      seriesCount: series.length,
      instanceCount,
    };

    if (mapped.studyInstanceUid.length === 0) {
      continue;
    }

    if (existing === undefined) {
      byPatient.set(medicalRecordNumber, {
        patient: {
          medicalRecordNumber,
          fullName: toDisplayName(patientTags.PatientName) || medicalRecordNumber,
          gender: toGender(patientTags.PatientSex),
          birthDate: toIsoDate(patientTags.PatientBirthDate),
        },
        studies: [mapped],
      });
    } else {
      existing.studies.push(mapped);
    }
  }

  for (const record of byPatient.values()) {
    record.studies.sort((left, right) => right.performedOn.localeCompare(left.performedOn));
  }

  return [...byPatient.values()];
}

async function getJson(path) {
  const response = await fetch(`${ORTHANC_URL}${path}`, {
    headers: orthancHeaders(),
  });
  if (!response.ok) {
    throw new Error(`GET ${path} failed with HTTP ${response.status}`);
  }
  return response.json();
}

/** DICOM DA is `YYYYMMDD`; the portal displays ISO calendar dates. */
function toIsoDate(value) {
  const text = String(value ?? '').trim();
  return /^\d{8}$/.test(text)
    ? `${text.slice(0, 4)}-${text.slice(4, 6)}-${text.slice(6, 8)}`
    : 'Unknown';
}

/**
 * DICOM PN components are caret-separated and ordered
 * family^given^middle^prefix^suffix. Word order differs by culture, so the
 * separators are simply replaced rather than reordered into a guess.
 */
function toDisplayName(value) {
  return String(value ?? '')
    .replaceAll('^', ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function toGender(value) {
  switch (
    String(value ?? '')
      .trim()
      .toUpperCase()
  ) {
    case 'M':
      return 'Male';
    case 'F':
      return 'Female';
    case 'O':
      return 'Other';
    default:
      return 'Unknown';
  }
}

async function writeFixtures(records) {
  const payload = {
    generatedAt: new Date().toISOString(),
    source: ORTHANC_URL,
    records,
  };

  await writeFile(FIXTURE_FILE, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');
}

await main();
