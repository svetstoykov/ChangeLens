export interface AnalysisStartRequest {
  readonly target: string;
  readonly freshnessToken: string;
  readonly changeContext: string | null;
}
