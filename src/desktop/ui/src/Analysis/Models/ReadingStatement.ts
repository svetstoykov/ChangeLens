import type { ReadingTrust } from "./ReadingTrust";

export interface ReadingStatement {
  readonly claimId: string;
  readonly text: string;
  readonly trust: ReadingTrust;
  readonly evidenceNodeIds: readonly string[];
}
