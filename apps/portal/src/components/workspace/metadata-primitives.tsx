import { cn } from '@cloudfilm/ui';

/**
 * The building blocks of the metadata rail.
 *
 * Extracted into their own module because both halves of the rail need them:
 * the archive's metadata and CloudFilm's own analysis are read in one column and
 * must sit on one grid. Importing them from the panel would have made the two
 * files circular - the panel renders the analysis section - so the shapes live
 * here and both sides import them.
 */

/**
 * A group of readings the reader opens when they want it.
 *
 * A native `<details>`, so it works before hydration, is keyboard operable for
 * free, and holds no state React has to own. Detailed metadata lives in these:
 * it matters, but it must not compete with the analysis module above it for a
 * reader's first glance.
 */
export function CollapsibleSection({
  title,
  defaultOpen = false,
  children,
}: {
  readonly title: string;
  readonly defaultOpen?: boolean;
  readonly children: React.ReactNode;
}) {
  return (
    <details
      open={defaultOpen}
      className="border-atrium-border group border-b last:border-b-0 [&_summary::-webkit-details-marker]:hidden"
    >
      {/* A group heading that happens to open, not a button: the row is the
          full width of the rail, the label keeps the eyebrow treatment every
          other section heading uses, and only the marker and a tint say it is
          interactive. */}
      <summary className="atrium-eyebrow text-atrium-muted hover:text-atrium-ink hover:bg-atrium-white/60 focus-visible:outline-atrium-primary group-open:text-atrium-ink flex cursor-pointer list-none items-center gap-2 rounded-lg px-4 py-2.5 transition-colors duration-[180ms] focus-visible:outline-2 focus-visible:-outline-offset-2">
        {/* The disclosure marker rotates rather than swapping glyph, so the
            control reads as one thing turning instead of two things blinking. */}
        <span
          aria-hidden="true"
          className="group-open:text-atrium-primary inline-block text-[13px] leading-none text-[var(--atrium-muted-2)] transition-transform duration-[180ms] group-open:rotate-90"
        >
          ›
        </span>
        {title}
      </summary>

      {/* The body fades in rather than appearing: a native `details` gives no
          height transition, and animating one on unknown content is the kind of
          thing that janks. Opacity is free. */}
      <div className="atrium-swap px-4 pt-1 pb-4">{children}</div>
    </details>
  );
}

/**
 * One block of readings, with a heading that stays put.
 *
 * The heading is `sticky` inside the rail's scroll container and carries its own
 * background, so rows pass underneath it rather than through it.
 */
export function MetadataSection({
  title,
  marker = false,
  children,
}: {
  readonly title: string;

  /**
   * Draws a dot before the heading.
   *
   * Marks a block as machine-derived rather than read from the file, which is
   * the one distinction a reader has to be able to make in this rail without
   * reading the heading.
   */
  readonly marker?: boolean;
  readonly children: React.ReactNode;
}) {
  return (
    <section className="border-atrium-border border-b last:border-b-0">
      <h3 className="bg-atrium-white/95 border-atrium-border/70 atrium-eyebrow text-atrium-muted sticky top-0 z-10 flex items-center gap-2 border-b px-5 py-2.5 backdrop-blur-[2px]">
        {marker ? (
          <span
            aria-hidden="true"
            className="bg-atrium-primary h-[5px] w-[5px] shrink-0 rounded-full"
          />
        ) : null}
        {title}
      </h3>
      <div className="px-5 py-4">{children}</div>
    </section>
  );
}

/**
 * One label and one value, on one line, in two fixed columns.
 *
 * The label column is fixed so every value in the rail starts at the same x,
 * which is what makes the column scannable vertically.
 */
export function MetadataRow({
  label,
  value,
  mono = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly mono?: boolean;
}) {
  return (
    <div className="grid grid-cols-[5.75rem_minmax(0,1fr)] items-baseline gap-x-3">
      <dt className="text-atrium-muted truncate text-[11.5px] leading-6">{label}</dt>
      <dd
        className={cn(
          'text-atrium-ink truncate text-[12.5px] leading-6',
          mono && 'font-mono text-[12px]',
        )}
        title={value}
      >
        {value}
      </dd>
    </div>
  );
}

/**
 * A value that has to be read whole.
 *
 * Study and Series UIDs are dotted decimal strings tens of characters long. In
 * the row grid they either truncated - which makes them wrong - or wrapped,
 * which dragged the label column out of alignment for every row beneath. Here
 * they get the full width of the rail, a recessed ground, and a break that is
 * allowed to fall anywhere because the value is never read as words.
 */
export function IdentifierBlock({
  label,
  value,
}: {
  readonly label: string;
  readonly value: string;
}) {
  return (
    <div className="mt-2 flex flex-col gap-1.5">
      <dt className="text-atrium-muted text-[11.5px] leading-6">{label}</dt>
      <dd className="border-atrium-border bg-atrium-panel text-atrium-ink rounded-lg border px-3 py-2 font-mono text-[11.5px] leading-5 break-all">
        {value}
      </dd>
    </div>
  );
}
