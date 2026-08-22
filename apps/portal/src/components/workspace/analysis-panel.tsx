'use client';

import { useState, useTransition } from 'react';
import type { AnalysisFinding, Locale, StudyAnalysis } from '@cloudfilm/shared';
import { Button, cn } from '@cloudfilm/ui';
import { useLocale, useTranslations } from '@/components/locale-provider';
import { analyseStudy, type AnalysisOutcome } from '@/app/analysis-actions';

/**
 * Automated characterisation of the study on screen.
 *
 * What this panel is careful about is what it does not say. The service reports
 * facts about the file - how many instances arrived, their geometry, their bit
 * depth, one pixel statistic - and none of that is a clinical finding. So the
 * results read as measurements beside the DICOM metadata they sit next to, the
 * disclaimer is on screen whenever a result is, and the vocabulary of diagnosis
 * appears nowhere: no severity, no colour coding, no badge whose tones mean
 * healthy and unhealthy.
 *
 * Nothing runs on mount. Analysis streams an entire study to another process and
 * is recorded as having done so, which is a thing a clinician asks for rather
 * than something that happens because they opened a page - and the workspace's
 * `[` and `]` shortcuts would otherwise fire one per keystroke. That holds even
 * when this study has never been analysed: an empty panel is the correct answer,
 * not a reason to send a patient's imaging somewhere.
 *
 * `initialAnalysis` is what the API had stored when the page was rendered, so a
 * result a clinician has already produced is on screen the moment the workspace
 * opens and survives a refresh. It is a starting value rather than a live one:
 * this component owns the state from mount onward, and a run replaces it.
 *
 * State lives here and nowhere else. The parent gives this component a `key` of
 * the study identifier, so React discards it when the reader moves to another
 * study and one study's measurements can never be read as another's - which is
 * also what makes seeding from a prop safe.
 */
export function AnalysisPanel({
  studyInstanceUid,
  initialAnalysis,
}: {
  readonly studyInstanceUid: string;

  /** What the API had stored for this study, or `null` if it has never been analysed. */
  readonly initialAnalysis: StudyAnalysis | null;
}) {
  const t = useTranslations();
  const locale = useLocale();

  const [outcome, setOutcome] = useState<AnalysisOutcome | null>(
    initialAnalysis === null ? null : { kind: 'analysed', analysis: initialAnalysis },
  );
  const [isRunning, startRunning] = useTransition();

  function run() {
    startRunning(async () => {
      setOutcome(await analyseStudy(studyInstanceUid));
    });
  }

  // Offering an action that this deployment cannot perform would be an invitation
  // to press it again. Every other outcome leaves the button, because retrying is
  // the reasonable response to all of them.
  const canRun = outcome?.kind !== 'not-configured';

  return (
    // Separated from the metadata above it by the same rule the metadata
    // sections use between themselves. This panel is a different kind of thing -
    // a machine reading rather than a fact from the file - but it is read in the
    // same column, and giving it a frame of its own would say it belongs to a
    // different screen.
    <section className="border-border mt-5 border-t pt-4">
      <h3 className="text-muted mb-2.5 text-[0.625rem] tracking-[0.08em] uppercase">
        {t('analysis.title')}
      </h3>

      {canRun ? (
        <div className="mb-3 flex flex-col gap-1.5">
          <Button
            variant="secondary"
            onClick={run}
            disabled={isRunning}
            aria-busy={isRunning}
            className="min-h-8 self-start px-2 text-xs"
          >
            {isRunning ? t('analysis.running') : t('analysis.run')}
          </Button>

          {/*
            Beside the button, always, rather than behind a confirmation nobody
            reads twice. The study leaves CloudFilm for another service when this
            is pressed, and the reader should know that before pressing it and not
            have to remember it afterwards.
          */}
          <p className="text-muted text-[0.6875rem] leading-relaxed">{t('analysis.disclosure')}</p>
        </div>
      ) : null}

      {outcome === null ? null : <Outcome outcome={outcome} locale={locale} onRetry={run} />}
    </section>
  );
}

/** Renders whichever of the four answers came back. */
function Outcome({
  outcome,
  locale,
  onRetry,
}: {
  readonly outcome: AnalysisOutcome;
  readonly locale: Locale;
  readonly onRetry: () => void;
}) {
  const t = useTranslations();

  if (outcome.kind === 'analysed') {
    return <Result analysis={outcome.analysis} locale={locale} />;
  }

  if (outcome.kind === 'not-configured') {
    return (
      <p className="text-muted text-[0.8125rem] leading-relaxed">{t('analysis.notConfigured')}</p>
    );
  }

  if (outcome.kind === 'unavailable') {
    return (
      <p className="text-muted text-[0.8125rem] leading-relaxed">{t('analysis.unavailable')}</p>
    );
  }

  return (
    <div className="flex flex-col items-start gap-2">
      {/*
        One sentence, chosen here and translated. Whatever the analysis service or
        the API said about the failure stays on the server: it has been through
        nothing that checked it, and it came from a process holding a patient's
        imaging.
      */}
      <p className="text-muted text-[0.8125rem] leading-relaxed">{t('analysis.failed')}</p>
      <Button variant="ghost" onClick={onRetry} className="min-h-8 px-2 text-xs">
        {t('common.retry')}
      </Button>
    </div>
  );
}

/** A completed analysis: what produced it, then what it observed. */
function Result({
  analysis,
  locale,
}: {
  readonly analysis: StudyAnalysis;
  readonly locale: Locale;
}) {
  const t = useTranslations();

  return (
    <div className="flex flex-col gap-3.5">
      {/*
        Attribution first and always. A measurement whose model and version are
        unknown cannot be reproduced, compared with a later run, or withdrawn when
        a model turns out to be wrong.
      */}
      <dl className="flex flex-col gap-1.5">
        <Row label={t('analysis.model')} value={analysis.modelName} />
        <Row label={t('analysis.version')} value={analysis.modelVersion} mono />
        <Row
          label={t('analysis.completed')}
          value={formatInstant(analysis.completedAtUtc, locale)}
        />
      </dl>

      {analysis.findings.length === 0 ? (
        // A real answer, not a failure: the model ran and named nothing.
        <p className="text-muted text-[0.8125rem] leading-relaxed">
          {t('analysis.noObservations')}
        </p>
      ) : (
        <div>
          <h4 className="text-muted mb-2 text-[0.625rem] tracking-[0.08em] uppercase">
            {t('analysis.observations')}
          </h4>
          <dl className="flex flex-col gap-1.5">
            {analysis.findings.map((finding) => (
              <Observation key={finding.label} finding={finding} />
            ))}
          </dl>
        </div>
      )}

      <p className="text-muted border-border border-t pt-3 text-[0.6875rem] leading-relaxed">
        {t('analysis.disclaimer')}
      </p>
    </div>
  );
}

/**
 * One observation, as a label and a value.
 *
 * The service states most observations as `name=value` - `rows=512`,
 * `modality=CT` - because the transport contract gives a finding a label and a
 * score and nothing else. Splitting them back apart is what makes them read as
 * measurements beside the study's own metadata rather than as machine output.
 *
 * A score of exactly 1 means "observed" and is not shown: rendering "100%" beside
 * `rows=512` would invent an uncertainty that does not exist and invite the
 * reader to treat the rest as probabilities. Anything else is the measurement
 * itself - the normalised pixel mean is the only one today - and is shown as the
 * value.
 */
function Observation({ finding }: { readonly finding: AnalysisFinding }) {
  const separator = finding.label.indexOf('=');
  const isMeasured = finding.score !== 1;

  // Split at the first '=' only: a value may legitimately contain another.
  const label = separator === -1 ? finding.label : finding.label.slice(0, separator);
  const stated = separator === -1 ? '' : finding.label.slice(separator + 1);

  const value = isMeasured ? formatScore(finding.score) : stated;

  return <Row label={label} value={value} mono />;
}

/**
 * One labelled value.
 *
 * The same two-column shape the metadata rows beside it use, so a measurement and
 * the study's own metadata line up on one grid. Kept local rather than imported,
 * because the panel that owns those helpers imports this component - sharing them
 * across the two files would make the import circular.
 */
function Row({
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
      <dt className="text-muted truncate text-[0.6875rem] leading-5">{label}</dt>
      <dd
        className={cn(
          'text-foreground truncate text-[0.8125rem] leading-5',
          mono && 'font-mono text-[0.75rem]',
        )}
        title={value}
      >
        {value.length > 0 ? value : '—'}
      </dd>
    </div>
  );
}

/**
 * Renders the completion instant in the reader's locale.
 *
 * Done here rather than on the server: this component renders only after a click,
 * so there is no server-rendered markup for a client-side format to disagree
 * with. An unparseable value is shown as it arrived rather than as "Invalid
 * Date" - the API's own timestamp is more use to whoever has to explain it.
 */
function formatInstant(value: string, locale: Locale): string {
  const parsed = Date.parse(value);

  if (Number.isNaN(parsed)) {
    return value;
  }

  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(parsed),
  );
}

/**
 * Renders a measured value.
 *
 * Fixed to four decimals, which is what the service's own rounding produces and
 * enough to tell two runs apart. Not a percentage: a percentage of nothing in
 * particular reads like a confidence, and this number is a normalised intensity.
 */
function formatScore(score: number): string {
  return score.toFixed(4);
}
