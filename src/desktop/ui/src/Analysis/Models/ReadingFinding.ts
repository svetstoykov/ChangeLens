import type { ReadingFindingSeverity } from "./ReadingFindingSeverity";

export interface ReadingFinding {
  readonly id: string;
  readonly severity: ReadingFindingSeverity;
  readonly title: string;
  readonly trigger: string;
  readonly impact: string;
  readonly fix: string;
  readonly areaId: string | null;
}
