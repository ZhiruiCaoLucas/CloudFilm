import type { Patient } from '@cloudfilm/shared';
import { DefinitionItem } from './definition-item';
import { serverTranslations } from '@/lib/locale';

/**
 * Patient identification banner.
 *
 * Name, MRN and date of birth stay together and stay at the top of the page:
 * they are the three fields a clinician checks to confirm they are looking at
 * the right person before reading anything below.
 *
 * A masthead closed by a rule rather than a bordered tile. This is the page's
 * subject, not one card among several, and boxing it made it read as though the
 * study list below belonged to something else.
 */
export async function PatientSummary({ patient }: { readonly patient: Patient }) {
  const t = await serverTranslations();

  return (
    <header className="border-border border-b pb-6">
      <h1 className="text-foreground text-xl font-semibold tracking-tight">{patient.fullName}</h1>
      <dl className="mt-5 grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-3">
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
    </header>
  );
}
