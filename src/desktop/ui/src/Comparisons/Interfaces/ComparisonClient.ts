import type { ComparisonFreshness } from "../Models/ComparisonFreshness";
import type { ComparisonTargetPage } from "../Models/ComparisonTargetPage";
import type { PreparedComparison } from "../Models/PreparedComparison";

export interface ComparisonClient {
  listTargets(request: {
    readonly path: string;
    readonly query?: string;
    readonly after?: string;
    readonly targetSetToken?: string;
  }): Promise<ComparisonTargetPage>;

  prepare(request: {
    readonly path: string;
    readonly target: string;
  }): Promise<PreparedComparison>;

  checkFreshness(request: {
    readonly path: string;
    readonly target: string;
    readonly freshnessToken: string;
  }): Promise<ComparisonFreshness>;
}
