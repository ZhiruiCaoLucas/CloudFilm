/**
 * One labelled value inside a definition list.
 *
 * `mono` marks the value as an identifier - an MRN, an accession number, a date.
 * Those are read character by character or compared down a column, which is the
 * only thing the technical face is for; everything else stays in the interface
 * face.
 */
export function DefinitionItem({
  label,
  value,
  mono = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly mono?: boolean;
}) {
  return (
    <div className="flex flex-col gap-2">
      <dt className="atrium-eyebrow text-atrium-muted">{label}</dt>
      <dd
        className={
          mono
            ? 'text-atrium-ink font-mono text-[13px] font-medium'
            : 'text-atrium-ink text-[14px] font-medium'
        }
      >
        {value}
      </dd>
    </div>
  );
}
