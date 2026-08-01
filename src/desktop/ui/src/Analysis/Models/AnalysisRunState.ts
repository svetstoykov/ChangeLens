export type AnalysisRunState =
  | "pendingCapture"
  | "capturing"
  | "discovering"
  | "collecting"
  | "persisting"
  | "completed"
  | "completedWithLimitations"
  | "cancelled"
  | "failed"
  | "interrupted";

export type AnalysisActiveStage = Extract<
  AnalysisRunState,
  "capturing" | "discovering" | "collecting" | "persisting"
>;

export const ANALYSIS_ACTIVE_STAGE_ORDER: readonly AnalysisActiveStage[] = [
  "capturing",
  "discovering",
  "collecting",
  "persisting",
];
