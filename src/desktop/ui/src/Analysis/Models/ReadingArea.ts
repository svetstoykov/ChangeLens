import type { ReadingShape } from "./ReadingShape";
import type { ReadingParticipant } from "./ReadingParticipant";
import type { ReadingRelationship } from "./ReadingRelationship";
import type { ReadingStatement } from "./ReadingStatement";

export interface ReadingArea {
  readonly id: string;
  readonly title: string;
  readonly summary: ReadingStatement | null;
  readonly shape: ReadingShape;
  readonly participants: readonly ReadingParticipant[];
  readonly relationships: readonly ReadingRelationship[];
  readonly orderedSteps: readonly ReadingStatement[];
  readonly purposes: readonly ReadingStatement[];
}
