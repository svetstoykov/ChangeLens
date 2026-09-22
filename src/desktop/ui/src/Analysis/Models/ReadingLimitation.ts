import type { ReadingLimitationKind } from "./ReadingLimitationKind";

export interface ReadingLimitation {
  readonly kind: ReadingLimitationKind;
  readonly path: string | null;
  readonly detail: string;
}
