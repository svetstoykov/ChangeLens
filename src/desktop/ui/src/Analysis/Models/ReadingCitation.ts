import type { ReadingFocusRange } from "./ReadingFocusRange";
import type { ReadingSide } from "./ReadingSide";
import type { ReadingTrust } from "./ReadingTrust";

export interface ReadingCitation {
  readonly claimId: string;
  readonly nodeId: string;
  readonly side: ReadingSide;
  readonly path: string;
  readonly objectId: string;
  readonly startLine: number;
  readonly endLine: number;
  readonly focus: readonly ReadingFocusRange[];
  readonly provenance: ReadingTrust;
}
