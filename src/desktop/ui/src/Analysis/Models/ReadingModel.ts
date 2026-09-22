import type { ReadingArea } from "./ReadingArea";
import type { ReadingAssurance } from "./ReadingAssurance";
import type { ReadingCitation } from "./ReadingCitation";
import type { ReadingEvidence } from "./ReadingEvidence";
import type { ReadingLimitation } from "./ReadingLimitation";
import type { ReadingOmissionSummary } from "./ReadingOmissionSummary";
import type { ReadingStatement } from "./ReadingStatement";

export interface ReadingModel {
  readonly thesis: ReadingStatement | null;
  readonly areas: readonly ReadingArea[];
  readonly citations: readonly ReadingCitation[];
  readonly evidence: readonly ReadingEvidence[];
  readonly limitations: readonly ReadingLimitation[];
  readonly omissionSummaries: readonly ReadingOmissionSummary[];
  readonly assurances: readonly ReadingAssurance[];
}
