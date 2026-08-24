import Link from 'next/link';
import { formatModality, type Patient, type Study } from '@cloudfilm/shared';
import { buttonClasses } from '@cloudfilm/ui';
import { CloudFilmLogo } from '@/components/cloudfilm-logo';
import { StudyThumbnail } from './study-thumbnail';
import { serverTranslations } from '@/lib/locale';

/**
 * Workstation title bar.
 *
 * Patient identity and study identity stay on screen for the whole reading
 * session. A radiologist reporting on the wrong study is the failure mode this
 * bar exists to prevent, so it never scrolls away.
 *
 * Three zones separated by full-height hairlines rather than one wrapping
 * definition list. The old bar laid four label/value pairs out in a flex wrap,
 * which meant the patient's name and the study's date could end up on different
 * lines at some widths and carried the same visual weight as one another. Now:
 *
 *   product mark │ WHO - the patient, at heading weight, with the MRN beneath
 *                │ WHAT - the study, as a modality tile, description and date
 *                                                              │ the way out
 *
 * "Who" and "what" are the two questions this bar answers, so they are the two
 * blocks it is made of, and the patient block is the heavier of the two.
 */
export async function WorkspaceHeader({
  patient,
  study,
}: {
  readonly patient: Patient;
  readonly study: Study;
}) {
  const t = await serverTranslations();

  return (
    <header className="border-atrium-border bg-atrium-white flex h-[68px] shrink-0 items-stretch border-b">
      {/* ---- product mark ------------------------------------------------ */}
      <div className="flex shrink-0 items-center px-5">
        <Link
          href="/"
          className="focus-visible:outline-atrium-primary flex items-center gap-2.5 rounded-lg focus-visible:outline-2 focus-visible:outline-offset-4"
        >
          <CloudFilmLogo className="h-7 w-7 shrink-0" />
          <span className="text-atrium-ink hidden text-[15px] font-semibold tracking-[-0.02em] xl:block">
            {t('common.appName')}
          </span>
        </Link>
      </div>

      <span aria-hidden="true" className="bg-atrium-border w-px shrink-0" />

      {/* ---- who: fixed, confirmed at a glance, never the widest zone --- */}
      <div className="flex max-w-[15rem] min-w-0 shrink-0 items-center gap-3 px-5">
        <div className="min-w-0">
          <span className="atrium-eyebrow text-atrium-muted block text-[10px]">
            {t('workspace.patient')}
          </span>
          <div className="mt-1 flex min-w-0 items-baseline gap-2.5">
            <Link
              href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
              className="text-atrium-ink hover:text-atrium-primary focus-visible:outline-atrium-primary truncate text-[15px] font-semibold tracking-[-0.02em] transition-colors duration-[180ms] focus-visible:outline-2 focus-visible:outline-offset-2"
            >
              {patient.fullName}
            </Link>
            {/* The MRN as a chip, not as another label/value pair. It is the
                identifier a clinician cross-checks against a worklist, so it is
                set apart from the name rather than beside it in the same tone. */}
            <span className="bg-atrium-panel border-atrium-border text-atrium-body shrink-0 rounded border px-1.5 py-0.5 font-mono text-[11px]">
              {patient.medicalRecordNumber}
            </span>
          </div>
        </div>
      </div>

      <span aria-hidden="true" className="bg-atrium-border hidden w-px shrink-0 md:block" />

      {/* ---- what ------------------------------------------------------- */}
      <div className="hidden min-w-0 flex-1 items-center gap-3 px-5 md:flex">
        <StudyThumbnail modality={study.modality} className="hidden h-9 w-9 lg:flex" />
        <div className="min-w-0">
          <span className="atrium-eyebrow text-atrium-muted block">{t('workspace.study')}</span>
          <div className="mt-1 flex min-w-0 items-baseline gap-2.5">
            <span className="text-atrium-ink truncate text-[14px] font-semibold tracking-[-0.015em]">
              {study.description}
            </span>
            <span className="text-atrium-body shrink-0 font-mono text-[11.5px] font-medium">
              {formatModality(study.modality)}
            </span>
            <span className="text-atrium-muted shrink-0 font-mono text-[11.5px]">
              {study.performedOn}
            </span>
          </div>
        </div>
      </div>

      {/* ---- the way out ------------------------------------------------- */}
      {/*
        The study's status used to repeat here. It now heads the right rail's
        context block, which is where a reader looks for the state of the thing
        on screen - and a fact stated twice in one viewport is a fact one of the
        two will eventually contradict.
      */}
      <div className="ml-auto flex shrink-0 items-center gap-4 px-5">
        <Link
          href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
          className={buttonClasses('secondary', undefined, 'sm')}
        >
          {t('workspace.backToPatient')}
        </Link>
      </div>
    </header>
  );
}
