import Link from 'next/link';
import { MODALITY_LABELS, STUDY_STATUS_LABELS, type Patient, type Study } from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import { StudyThumbnail } from './study-thumbnail';

/**
 * Left rail: who the patient is, and every study available for them.
 *
 * Each study is an anchor rather than a click handler. Selecting a study is a
 * change of address, so it must be linkable, middle-clickable, and reachable by
 * Tab — all of which come free from `<a>` and none of which come free from a
 * `div` with an `onClick`.
 */
export function StudyNavigator({
  patient,
  studies,
  selectedStudyInstanceUid,
}: {
  readonly patient: Patient;
  readonly studies: readonly Study[];
  readonly selectedStudyInstanceUid: string;
}) {
  return (
    <aside
      aria-label="Study navigator"
      className="border-border bg-surface-raised flex w-80 shrink-0 flex-col overflow-y-auto border-r"
    >
      <section className="border-border shrink-0 border-b px-4 py-4">
        <h2 className="text-muted mb-2 text-xs tracking-wide uppercase">Patient</h2>
        <p className="text-foreground text-sm font-medium">{patient.fullName}</p>
        <dl className="text-muted mt-1 space-y-0.5 text-xs">
          <div className="flex gap-2">
            <dt>MRN</dt>
            <dd className="text-foreground font-mono">{patient.medicalRecordNumber}</dd>
          </div>
          <div className="flex gap-2">
            <dt>Born</dt>
            <dd className="text-foreground font-mono">{patient.birthDate}</dd>
          </div>
          <div className="flex gap-2">
            <dt>Gender</dt>
            <dd className="text-foreground">{patient.gender}</dd>
          </div>
        </dl>
      </section>

      <nav aria-label="Studies" className="flex-1 px-2 py-3">
        <h2 className="text-muted px-2 pb-2 text-xs tracking-wide uppercase">
          Studies ({studies.length})
        </h2>
        <ul className="flex flex-col gap-1">
          {studies.map((study) => (
            <li key={study.studyInstanceUid}>
              <StudyNavigatorItem
                study={study}
                isSelected={study.studyInstanceUid === selectedStudyInstanceUid}
              />
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}

function StudyNavigatorItem({
  study,
  isSelected,
}: {
  readonly study: Study;
  readonly isSelected: boolean;
}) {
  return (
    <Link
      href={`/workspace/${encodeURIComponent(study.studyInstanceUid)}`}
      aria-current={isSelected ? 'page' : undefined}
      className={cn(
        'focus-visible:outline-accent flex w-full items-start gap-3 rounded-md px-2 py-2 text-left transition-colors',
        'focus-visible:outline-2 focus-visible:-outline-offset-2',
        isSelected ? 'bg-accent/10 ring-accent/40 ring-1 ring-inset' : 'hover:bg-surface-overlay',
      )}
    >
      <StudyThumbnail modality={study.modality} />

      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="text-foreground truncate text-sm font-medium">
          {MODALITY_LABELS[study.modality]}
        </span>
        <span className="text-muted truncate text-xs">{study.description}</span>
        <span className="text-muted flex items-center gap-2 font-mono text-xs">
          <span>{study.performedOn}</span>
          <span aria-hidden="true">·</span>
          <span>{STUDY_STATUS_LABELS[study.status]}</span>
        </span>
      </span>
    </Link>
  );
}
