import { IS_SERVING_SEEDED_DATA } from '@cloudfilm/shared/mock';

/**
 * Says plainly when the listed studies are placeholders.
 *
 * Without it the portal looks fully populated while every study is unopenable,
 * and the first sign of trouble is an empty viewer. In a clinical application
 * the difference between "no data" and "data you cannot see" must never be left
 * for the user to infer.
 *
 * Renders nothing once real studies have been imported.
 */
export function SampleDataNotice() {
  if (IS_SERVING_SEEDED_DATA) {
    return null;
  }

  return (
    <aside
      aria-label="Sample data notice"
      className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-5 py-4"
    >
      <p className="text-sm font-medium text-amber-700 dark:text-amber-400">
        No imaging data has been imported
      </p>
      <p className="text-muted mt-1 text-sm">
        The studies below are placeholders with synthetic identifiers, so the viewer cannot open
        them. To load a real study into the local Orthanc:
      </p>
      <pre className="bg-surface-overlay text-foreground mt-3 overflow-x-auto rounded-md p-3 font-mono text-xs leading-relaxed">
        {
          '# put DICOM in services/orthanc/seed/, then\ndocker compose -f docker-compose.yml -f docker-compose.dev.yml up -d\npnpm seed:orthanc\npnpm verify:imaging'
        }
      </pre>
    </aside>
  );
}
