import type { AnalysisRunProjection } from "./AnalysisRunProjection";

export type AnalysisGetActiveResult =
  | { readonly state: "none" }
  | { readonly state: "active"; readonly run: AnalysisRunProjection };
