import type { AnalysisGetActiveResult } from "../Models/AnalysisGetActiveResult";
import type { AnalysisRunProjection } from "../Models/AnalysisRunProjection";
import type { AnalysisStartResult } from "../Models/AnalysisStartResult";

export interface AnalysisClient {
  start(
    path: string,
    target: string,
    freshnessToken: string,
    changeContext: string | null,
  ): Promise<AnalysisStartResult>;
  getActive(path: string): Promise<AnalysisGetActiveResult>;
  pollRun(runId: string): Promise<AnalysisRunProjection>;
  cancel(runId: string): Promise<void>;
}
