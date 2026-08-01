export type AnalysisStartResult =
  | {
      readonly state: "accepted";
      readonly runId: string;
      readonly requestedAt: number;
    }
  | { readonly state: "rejectedStale" }
  | { readonly state: "rejectedActive"; readonly activeRunId: string };
