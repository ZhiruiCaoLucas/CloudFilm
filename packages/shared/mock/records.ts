/**
 * The fixture set's shapes are the API's shapes.
 *
 * Re-exported rather than redefined: these are transport contracts and belong in
 * `src/`. The mock satisfies the same contract the API does, which is what lets
 * it stand in during development and testing.
 */
export type { PatientRecord, StudyContext } from '../src/index';
