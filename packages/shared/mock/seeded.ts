import type { Patient, PatientGender, Study, StudyStatus } from '../src/index';
import type { PatientRecord } from './records';
import seededStudies from './seeded-studies.json';

/**
 * Records discovered in a local Orthanc by `pnpm seed:orthanc`.
 *
 * Empty in a fresh checkout, and empty is the normal state: the portal must run
 * with no Docker at all. When the seed script has run, these carry the *real*
 * Study Instance UIDs of the DICOM sitting in Orthanc, which is what lets the
 * viewer actually open a study instead of being handed a fabricated UID.
 *
 * The file is generated, so it is parsed defensively rather than trusted. A
 * malformed entry is dropped instead of crashing every page that renders a
 * study list.
 */
const GENDERS: readonly PatientGender[] = ['Male', 'Female', 'Other', 'Unknown'];
const STATUSES: readonly StudyStatus[] = ['Completed', 'InProgress', 'Cancelled'];

export const SEEDED_RECORDS: readonly PatientRecord[] = parseRecords(seededStudies.records);

/** When the fixtures were generated, or `null` when none have been. */
export const SEEDED_AT: string | null = seededStudies.generatedAt;

function parseRecords(value: unknown): readonly PatientRecord[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap((entry) => {
    const record = toRecord(entry);
    return record === undefined ? [] : [record];
  });
}

function toRecord(value: unknown): PatientRecord | undefined {
  const entry = asObject(value);
  if (entry === undefined) {
    return undefined;
  }

  const patient = toPatient(entry['patient']);
  const rawStudies = entry['studies'];

  if (patient === undefined || !Array.isArray(rawStudies)) {
    return undefined;
  }

  const studies = rawStudies.flatMap((study) => {
    const parsed = toStudy(study);
    return parsed === undefined ? [] : [parsed];
  });

  return studies.length > 0 ? { patient, studies } : undefined;
}

function toPatient(value: unknown): Patient | undefined {
  const entry = asObject(value);
  if (entry === undefined) {
    return undefined;
  }

  const medicalRecordNumber = asText(entry['medicalRecordNumber']);
  const fullName = asText(entry['fullName']);

  if (medicalRecordNumber === undefined || fullName === undefined) {
    return undefined;
  }

  return {
    medicalRecordNumber,
    fullName,
    gender: asMember(entry['gender'], GENDERS) ?? 'Unknown',
    birthDate: asText(entry['birthDate']) ?? 'Unknown',
  };
}

function toStudy(value: unknown): Study | undefined {
  const entry = asObject(value);
  if (entry === undefined) {
    return undefined;
  }

  const studyInstanceUid = asText(entry['studyInstanceUid']);
  if (studyInstanceUid === undefined) {
    return undefined;
  }

  return {
    studyInstanceUid,
    modality: asText(entry['modality']) ?? 'OT',
    description: asText(entry['description']) ?? 'Untitled study',
    performedOn: asText(entry['performedOn']) ?? 'Unknown',
    status: asMember(entry['status'], STATUSES) ?? 'Completed',
    seriesCount: asCount(entry['seriesCount']),
    instanceCount: asCount(entry['instanceCount']),
  };
}

function asObject(value: unknown): Record<string, unknown> | undefined {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function asText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

function asMember<T extends string>(value: unknown, allowed: readonly T[]): T | undefined {
  return typeof value === 'string' && (allowed as readonly string[]).includes(value)
    ? (value as T)
    : undefined;
}

function asCount(value: unknown): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : 0;
}
