import type { AnalysisRunSummary } from "./AnalysisRunSummary";

export type AnalysisGetActiveResult =
  | { readonly state: "none" }
  | { readonly state: "active"; readonly run: AnalysisRunSummary };
