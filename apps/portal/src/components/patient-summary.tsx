import type { Patient } from '@cloudfilm/shared';
import { Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { DefinitionItem } from './definition-item';

/**
 * Patient identification banner.
 *
 * Name, MRN and date of birth stay together and stay at the top of the page:
 * they are the three fields a clinician checks to confirm they are looking at
 * the right person before reading anything below.
 */
export function PatientSummary({ patient }: { readonly patient: Patient }) {
  return (
    <Card>
      <CardHeader>
        <h1 className="text-foreground text-xl font-semibold tracking-tight">{patient.fullName}</h1>
      </CardHeader>
      <CardBody>
        <dl className="grid grid-cols-1 gap-6 sm:grid-cols-3">
          <DefinitionItem label="Medical record number" value={patient.medicalRecordNumber} />
          <DefinitionItem label="Gender" value={patient.gender} />
          <DefinitionItem label="Birth date" value={patient.birthDate} />
        </dl>
      </CardBody>
    </Card>
  );
}
