/**
 * One series within an imaging study.
 *
 * A study is a visit; a series is one acquisition within it. Radiologists read
 * at series granularity — "the axial T2", not "the MRI" — so the workspace has
 * to be able to name what the viewer is showing.
 *
 * Read from a QIDO-RS query of `/studies/{uid}/series`, which is why series are
 * carried on {@link StudyContext} rather than on {@link Study}: they are
 * resolved for the one study being opened. Putting them on every study would
 * mean one extra archive query per sibling study, for data nothing displays.
 */
export interface Series {
  /** DICOM Series Instance UID (`0020000E`). */
  readonly seriesInstanceUid: string;

  /**
   * DICOM Series Number (`00200011`), or zero when the archive reports none.
   *
   * Type 2 in DICOM, so a series legitimately carries no number. Zero is not a
   * valid DICOM series number, which makes it usable as "not reported" without
   * a second field — but it must be rendered as absence, never as "Series 0".
   */
  readonly seriesNumber: number;

  /** DICOM modality code for this series (`00080060`). Use `formatModality` to render it. */
  readonly modality: string;

  /**
   * DICOM Series Description (`0008103E`), or an empty string when absent.
   *
   * No placeholder text. `Study.description` substitutes "Untitled study"
   * because a study list needs a row label; a metadata row does not, and an
   * invented description on a clinical record is worse than a missing one.
   */
  readonly description: string;

  /**
   * Number of images in this series (`00201209`), or zero when not reported.
   *
   * Read from the series query. Counting instances individually would mean one
   * request per series to learn a number the archive already publishes.
   */
  readonly instanceCount: number;

  /**
   * DICOM Institution Name (`00080080`), or an empty string when absent.
   *
   * Recorded per series, in the General Equipment module — not at study level —
   * because the equipment that produced each series is what carries it. Type 3,
   * so plenty of archives omit it entirely.
   */
  readonly institutionName: string;
}
