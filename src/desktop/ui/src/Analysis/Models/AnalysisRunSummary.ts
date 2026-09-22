import type { AnalysisComparison } from "./AnalysisComparison";
import type { AnalysisFact } from "./AnalysisFact";
import type { AnalysisRepository } from "./AnalysisRepository";
import type { AnalysisRunState } from "./AnalysisRunState";
import type { AnalysisTerminalSummary } from "./AnalysisTerminalSummary";
import type { ReadingModel } from "./ReadingModel";
import type { ValidationRemoval } from "./ValidationRemoval";

export interface AnalysisRunSummary {
  readonly runId: string;
  readonly state: AnalysisRunState;
  readonly repository: AnalysisRepository;
  readonly comparison: AnalysisComparison;
  readonly requestedAt: number;
  readonly captureStartedAt: number | null;
  readonly capturedAt: number | null;
  readonly snapshotId: string | null;
  readonly cancellationRequested: boolean;
  readonly facts: readonly AnalysisFact[];
  readonly terminal: AnalysisTerminalSummary | null;
  readonly interruptedAt: number | null;
  readonly interruptionReason: "engineStopped" | null;
  readonly readingModel: ReadingModel | null;
  readonly validationRemovals: readonly ValidationRemoval[] | null;
}
