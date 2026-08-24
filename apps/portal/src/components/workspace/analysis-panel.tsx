'use client';

import { useState, useTransition } from 'react';
import type { AnalysisFinding, Locale, MessageKey, StudyAnalysis } from '@cloudfilm/shared';
import { Button, StatusBadge, type StatusTone } from '@cloudfilm/ui';
import { useLocale, useTranslations } from '@/components/locale-provider';
import { MetadataRow } from './metadata-primitives';
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
  const stateTone = stateOf(outcome, isRunning);

  const hasResult = outcome?.kind === 'analysed';

  return (
    /*
      A module, not a metadata section.
      -------------------------------------------------------------------------
      This used to be the last block in a long scrolling rail, styled exactly
      like the archive metadata above it - which meant a clinician had to scroll
      past four groups of DICOM values to discover that analysis existed at all.
      It now sits directly under the study context at the top of the rail, on its
      own surface, with the action as the largest thing in it.

      Three questions, answered top to bottom in one glance: what this is, whether
      it has been run, and what can be done next.
    */
    <section
      aria-labelledby="analysis-heading"
      className="border-atrium-border-strong bg-atrium-white m-4 rounded-xl border shadow-[var(--shadow-md)] transition-shadow duration-[220ms] hover:shadow-[var(--shadow-lg)]"
    >
      <div className="border-atrium-border bg-atrium-sky-100/45 flex items-center justify-between gap-2 rounded-t-xl border-b px-4 py-3">
        <h3
          id="analysis-heading"
          className="atrium-eyebrow text-atrium-muted flex items-center gap-2"
        >
          {/* The machine marker. A dot, not an icon: the one thing a reader has
              to be able to tell about this block is that its contents were
              derived rather than read out of the file. */}
          <span
            aria-hidden="true"
            className="bg-atrium-primary h-[5px] w-[5px] shrink-0 rounded-full"
          />
          {t('workspace.analysisSection')}
        </h3>

        {/* The state, as a token, so "has this been run" is answerable without
            reading a word of the body. */}
        {stateTone === null ? null : (
          <StatusBadge
            tone={stateTone.tone}
            // The one continuous animation in CloudFilm, and it is the one that
            // earns it: it runs only while a request is in flight, it is on a
            // 5px dot, and it stops the moment the answer arrives.
            className={
              isRunning
                ? '[&>span:first-child]:animate-[atrium-breathe_1.6s_ease-in-out_infinite]'
                : ''
            }
          >
            {t(stateTone.label)}
          </StatusBadge>
        )}
      </div>

      <div className="px-4 py-4">
        <p className="text-atrium-ink text-[13.5px] font-semibold tracking-[-0.015em]">
          {t('analysis.title')}
        </p>

        {/* What it does, before it has done anything. Replaced by the result
            once there is one, so the module never carries both. */}
        {hasResult ? null : (
          <p className="text-atrium-body mt-1.5 text-[12.5px] leading-relaxed">
            {outcome === null ? t('analysis.emptyBody') : null}
          </p>
        )}

        {outcome === null ? null : (
          <div className="mt-3">
            <Outcome outcome={outcome} locale={locale} />
          </div>
        )}

        {canRun ? (
          <div className="mt-4 flex flex-col gap-2">
            <Button
              variant={hasResult ? 'secondary' : 'primary'}
              size={hasResult ? 'sm' : 'md'}
              onClick={run}
              disabled={isRunning}
              aria-busy={isRunning}
              className={hasResult ? 'self-start' : 'w-full'}
            >
              {isRunning
                ? t('analysis.running')
                : hasResult
                  ? t('analysis.rerun')
                  : t('analysis.run')}
            </Button>

            {/*
              Beside the button, always, rather than behind a confirmation nobody
              reads twice. The study leaves CloudFilm for another service when
              this is pressed, and the reader should know that before pressing it
              and not have to remember it afterwards.
            */}
            <p className="text-atrium-muted text-[11px] leading-relaxed">
              {t('analysis.disclosure')}
            </p>
          </div>
        ) : null}
      </div>
    </section>
  );
}

/**
 * The state token for the module header.
 *
 * Null while nothing has happened: a badge reading "not started" is a badge
 * saying nothing, and the body already says it in words.
 */
function stateOf(
  outcome: AnalysisOutcome | null,
  isRunning: boolean,
): { readonly tone: StatusTone; readonly label: MessageKey } | null {
  if (isRunning) {
    return { tone: 'degraded', label: 'analysis.stateRunning' };
  }

  if (outcome === null) {
    return null;
  }

  if (outcome.kind === 'analysed') {
    return { tone: 'healthy', label: 'analysis.stateCompleted' };
  }

  if (outcome.kind === 'failed') {
    return { tone: 'unhealthy', label: 'analysis.stateFailed' };
  }

  return null;
}

/** Renders whichever of the four answers came back. */
function Outcome({
  outcome,
  locale,
}: {
  readonly outcome: AnalysisOutcome;
  readonly locale: Locale;
}) {
  const t = useTranslations();

  if (outcome.kind === 'analysed') {
    return <Result analysis={outcome.analysis} locale={locale} />;
  }

  if (outcome.kind === 'not-configured') {
    return (
      <p className="text-atrium-body text-[12.5px] leading-relaxed">
        {t('analysis.notConfigured')}
      </p>
    );
  }

  if (outcome.kind === 'unavailable') {
    return (
      <p className="text-atrium-body text-[12.5px] leading-relaxed">{t('analysis.unavailable')}</p>
    );
  }

  /*
    One sentence, chosen here and translated. Whatever the analysis service or
    the API said about the failure stays on the server: it has been through
    nothing that checked it, and it came from a process holding a patient's
    imaging.

    No retry control here: the module below owns the action, and two buttons
    doing the same thing a few pixels apart is how a reader ends up pressing the
    wrong one.
  */
  return <p className="text-atrium-body text-[12.5px] leading-relaxed">{t('analysis.failed')}</p>;
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
    <div className="flex flex-col gap-4">
      {/*
        Attribution first and always. A measurement whose model and version are
        unknown cannot be reproduced, compared with a later run, or withdrawn when
        a model turns out to be wrong.
      */}
      <dl className="flex flex-col gap-2">
        <MetadataRow label={t('analysis.model')} value={analysis.modelName} />
        <MetadataRow label={t('analysis.version')} value={analysis.modelVersion} mono />
        <MetadataRow
          label={t('analysis.completed')}
          value={formatInstant(analysis.completedAtUtc, locale)}
        />
      </dl>

      {analysis.findings.length === 0 ? (
        // A real answer, not a failure: the model ran and named nothing.
        <p className="text-atrium-body text-[12.5px] leading-relaxed">
          {t('analysis.noObservations')}
        </p>
      ) : (
        <div>
          <h4 className="atrium-eyebrow text-atrium-muted mb-2.5">{t('analysis.observations')}</h4>
          <dl className="flex flex-col gap-2">
            {analysis.findings.map((finding) => (
              <Observation key={finding.label} finding={finding} />
            ))}
          </dl>
        </div>
      )}

      {/* Compliance copy. Never shortened, never removed, and always on screen
          whenever a result is. */}
      <p className="text-atrium-muted border-atrium-border mt-1 border-t pt-3 text-[11px] leading-relaxed italic">
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

  return <MetadataRow label={label} value={value} mono />;
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
