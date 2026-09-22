import type { ReadingAssuranceKind } from "./ReadingAssuranceKind";

export interface ReadingAssurance {
  readonly kind: ReadingAssuranceKind;
  readonly detail: string;
  readonly claimId: string | null;
}
