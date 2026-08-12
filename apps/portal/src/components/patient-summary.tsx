import type { Patient } from '@cloudfilm/shared';
import { Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { DefinitionItem } from './definition-item';
import { serverTranslations } from '@/lib/locale';

/**
 * Patient identification banner.
 *
 * Name, MRN and date of birth stay together and stay at the top of the page:
 * they are the three fields a clinician checks to confirm they are looking at
 * the right person before reading anything below.
 */
export async function PatientSummary({ patient }: { readonly patient: Patient }) {
  const t = await serverTranslations();

  return (
    <Card>
      <CardHeader>
        <h1 className="text-foreground text-xl font-semibold tracking-tight">{patient.fullName}</h1>
      </CardHeader>
      <CardBody>
        <dl className="grid grid-cols-1 gap-6 sm:grid-cols-3">
          <DefinitionItem
            label={t('patient.medicalRecordNumber')}
            value={patient.medicalRecordNumber}
          />
          {/* The gender label and its value are both UI vocabulary derived from
              a DICOM code, so both are translated. The birth date is archive
              data and is rendered exactly as the archive reported it. */}
          <DefinitionItem label={t('patient.gender')} value={t(`gender.${patient.gender}`)} />
          <DefinitionItem label={t('patient.birthDate')} value={patient.birthDate} />
        </dl>
      </CardBody>
    </Card>
  );
}
