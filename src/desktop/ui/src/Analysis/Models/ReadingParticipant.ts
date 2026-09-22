export interface ReadingParticipant {
  readonly id: string;
  readonly name: string;
  readonly role: string;
  readonly changed: boolean;
  readonly evidenceNodeIds: readonly string[];
}
