import type { ComparisonTargetKind } from "./ComparisonTargetKind";

export interface ComparisonTarget {
  readonly kind: ComparisonTargetKind;
  readonly name: string;
  readonly fullName: string;
  readonly revision: string;
}
