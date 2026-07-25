import type { ComparisonTarget } from "./ComparisonTarget";

export interface ComparisonTargetPage {
  readonly targets: readonly ComparisonTarget[];
  readonly suggestedTarget: ComparisonTarget | null;
  readonly nextCursor: string | null;
  readonly targetSetToken: string;
  readonly unsupportedTargetCount: number;
}
