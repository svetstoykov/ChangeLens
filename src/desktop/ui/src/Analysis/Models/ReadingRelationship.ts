import type { ReadingTrust } from "./ReadingTrust";

export interface ReadingRelationship {
  readonly claimId: string;
  readonly id: string;
  readonly fromParticipantId: string;
  readonly toParticipantId: string;
  readonly kind: string;
  readonly explanation: string;
  readonly trust: ReadingTrust;
  readonly evidenceNodeIds: readonly string[];
}
