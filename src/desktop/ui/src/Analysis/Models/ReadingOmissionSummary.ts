import type { ReadingOmissionSourceKind } from "./ReadingOmissionSourceKind";

export interface ReadingOmissionSummary {
  readonly sourceKind: ReadingOmissionSourceKind;
  readonly reason: string;
  readonly totalCount: number;
  readonly sampleCount: number;
  readonly resolvedSampleCount: number;
}
