/** One labelled value inside a definition list. */
export function DefinitionItem({
  label,
  value,
}: {
  readonly label: string;
  readonly value: string;
}) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-muted text-[0.625rem] tracking-[0.08em] uppercase">{label}</dt>
      <dd className="text-foreground text-[0.8125rem] font-medium">{value}</dd>
    </div>
  );
}
