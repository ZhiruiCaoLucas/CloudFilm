import { cn } from '@cloudfilm/ui';

/**
 * Stand-in for a study's key image.
 *
 * A real thumbnail is a rendered DICOM frame, which requires the viewer. The
 * placeholder keeps the navigator's layout and rhythm identical to what a real
 * image would occupy, so swapping one in changes pixels and not structure.
 *
 * Atrium draws it as a dark, faintly dot-textured tile carrying the modality
 * code — the one place in the light chrome that echoes the viewport, which is
 * what makes a list of these read as a list of images rather than of rows.
 */
export function StudyThumbnail({
  modality,
  className,
}: {
  readonly modality: string;
  readonly className?: string;
}) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'atrium-tile-interactive relative flex h-[50px] w-[50px] shrink-0 items-center justify-center overflow-hidden rounded-md',
        'bg-[var(--atrium-tile)] shadow-[inset_0_0_0_1px_rgba(157,192,247,0.28)]',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="absolute inset-0 opacity-40"
        style={{
          backgroundImage: 'radial-gradient(circle, rgba(157,192,247,0.35) 0.5px, transparent 1px)',
          backgroundSize: '6px 6px',
        }}
      />
      <span className="relative font-mono text-[10px] font-medium tracking-[0.08em] text-[var(--atrium-sky-300)]">
        {modality.toUpperCase()}
      </span>
    </div>
  );
}
