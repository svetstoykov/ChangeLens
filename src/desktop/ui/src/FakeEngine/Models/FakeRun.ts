import type { PreparedComparison } from "../../Comparisons/Models/PreparedComparison";

export interface FakeRun {
  readonly runId: string;
  readonly path: string;
  readonly prepared: PreparedComparison;
  readonly requestedAt: number;
  cancelledAt: number | null;
}
