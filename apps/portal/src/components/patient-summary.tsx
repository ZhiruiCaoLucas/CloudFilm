import type { Patient } from '@cloudfilm/shared';
import { CloudFilmLogo } from './cloudfilm-logo';
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
    <header className="border-atrium-border border-b pb-7">
      <div className="flex min-w-0 items-center gap-4">
        {/* An icon tile, not an avatar. CloudFilm holds no photograph of a
            patient and the masthead should not imply that it does. */}
        <span
          aria-hidden="true"
          className="border-atrium-border bg-atrium-sky-100 inline-flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl border"
        >
          <CloudFilmLogo className="h-7 w-7" />
        </span>

        <h1 className="atrium-h1 text-atrium-ink min-w-0 truncate">{patient.fullName}</h1>
      </div>

      <dl className="mt-7 grid grid-cols-1 gap-x-10 gap-y-5 sm:grid-cols-3">
        <DefinitionItem
          label={t('patient.medicalRecordNumber')}
          value={patient.medicalRecordNumber}
          mono
        />
        {/* The gender label and its value are both UI vocabulary derived from
            a DICOM code, so both are translated. The birth date is archive
            data and is rendered exactly as the archive reported it. */}
        <DefinitionItem label={t('patient.gender')} value={t(`gender.${patient.gender}`)} />
        <DefinitionItem label={t('patient.birthDate')} value={patient.birthDate} mono />
      </dl>
    </header>
  );
}
