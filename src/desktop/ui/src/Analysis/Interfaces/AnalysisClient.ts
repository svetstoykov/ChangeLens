import type { AnalysisGetActiveResult } from "../Models/AnalysisGetActiveResult";
import type { AnalysisRunSummary } from "../Models/AnalysisRunSummary";
import type { AnalysisStartResult } from "../Models/AnalysisStartResult";

export interface AnalysisClient {
  start(
    path: string,
    target: string,
    freshnessToken: string,
    changeContext: string | null,
  ): Promise<AnalysisStartResult>;
  getActive(path: string): Promise<AnalysisGetActiveResult>;
  pollRun(runId: string): Promise<AnalysisRunSummary>;
  cancel(runId: string): Promise<void>;
}
