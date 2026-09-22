import type { ReadingParticipant } from "../Models/ReadingParticipant";

export interface ReadingParticipantListProps {
  readonly participants: readonly ReadingParticipant[];
}

export function ReadingParticipantList({
  participants,
}: ReadingParticipantListProps) {
  if (participants.length === 0) return null;

  return (
    <ul className="reading-participants">
      {participants.map((participant) => (
        <li key={participant.id}>
          <span className="reading-participant-name">{participant.name}</span>
          <span className="reading-participant-role">{participant.role}</span>
          {participant.changed ? (
            <span className="reading-participant-changed">changed</span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
