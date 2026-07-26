import type { StudyModality } from '@cloudfilm/shared';

/**
 * Stand-in for a study's key image.
 *
 * A real thumbnail is a rendered DICOM frame, which requires the viewer. The
 * placeholder keeps the navigator's layout and rhythm identical to what Sprint 3
 * will show, so swapping in real images changes pixels and not structure.
 */
export function StudyThumbnail({ modality }: { readonly modality: StudyModality }) {
  return (
    <div
      aria-hidden="true"
      className="border-border flex h-14 w-14 shrink-0 items-center justify-center rounded border bg-black/90"
    >
      <span className="font-mono text-[0.625rem] font-medium tracking-wider text-slate-400">
        {modality}
      </span>
    </div>
  );
}
