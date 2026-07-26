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
      <dt className="text-muted text-xs tracking-wide uppercase">{label}</dt>
      <dd className="text-foreground text-sm font-medium">{value}</dd>
    </div>
  );
}
