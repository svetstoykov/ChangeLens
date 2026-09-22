import type { ReadingArea } from "../Models/ReadingArea";
import type { ReadingCitation } from "../Models/ReadingCitation";
import type { ReadingEvidence } from "../Models/ReadingEvidence";
import type { ReadingShape } from "../Models/ReadingShape";
import { ReadingClaimQuotes } from "./ReadingClaimQuotes";
import { ReadingParticipantList } from "./ReadingParticipantList";

export interface ReadingAreaSectionProps {
  readonly area: ReadingArea;
  readonly citations: readonly ReadingCitation[];
  readonly evidence: readonly ReadingEvidence[];
}

export function ReadingAreaSection({
  area,
  citations,
  evidence,
}: ReadingAreaSectionProps) {
  const participantsById = new Map(
    area.participants.map(
      (participant) => [participant.id, participant] as const,
    ),
  );
  const nameFor = (participantId: string): string =>
    participantsById.get(participantId)?.name ?? participantId;

  return (
    <section className="reading-area" aria-label={area.title}>
      <header className="reading-area-heading">
        <h3>{area.title}</h3>
        <span className="reading-shape-label">{describeShape(area.shape)}</span>
      </header>
      {area.summary !== null ? (
        <div className="reading-statement">
          <p className="reading-statement-text">{area.summary.text}</p>
          <ReadingClaimQuotes
            claimId={area.summary.claimId}
            citations={citations}
            evidence={evidence}
          />
        </div>
      ) : null}
      <ReadingParticipantList participants={area.participants} />
      {area.shape === "walk" && area.orderedSteps.length > 0 ? (
        <ol className="reading-steps">
          {area.orderedSteps.map((step, index) => (
            <li key={step.claimId} className="reading-step">
              <span className="reading-step-number" aria-hidden="true">
                {index + 1}
              </span>
              <div className="reading-step-body">
                <p className="reading-statement-text">{step.text}</p>
                <ReadingClaimQuotes
                  claimId={step.claimId}
                  citations={citations}
                  evidence={evidence}
                />
              </div>
            </li>
          ))}
        </ol>
      ) : null}
      {area.shape === "purposeCards" && area.purposes.length > 0 ? (
        <ul className="reading-purposes">
          {area.purposes.map((purpose) => (
            <li key={purpose.claimId} className="reading-purpose">
              <p className="reading-statement-text">{purpose.text}</p>
              <ReadingClaimQuotes
                claimId={purpose.claimId}
                citations={citations}
                evidence={evidence}
              />
            </li>
          ))}
        </ul>
      ) : null}
      {area.shape === "participantMap" && area.relationships.length > 0 ? (
        <ul className="reading-relationships">
          {area.relationships.map((relationship) => (
            <li key={relationship.id} className="reading-relationship">
              <p className="reading-relationship-line">
                <span className="reading-relationship-endpoint">
                  {nameFor(relationship.fromParticipantId)}
                </span>
                <span className="reading-relationship-kind">
                  {relationship.kind}
                </span>
                <span className="reading-relationship-endpoint">
                  {nameFor(relationship.toParticipantId)}
                </span>
              </p>
              <p className="reading-relationship-explanation">
                {relationship.explanation}
              </p>
              <ReadingClaimQuotes
                claimId={relationship.claimId}
                citations={citations}
                evidence={evidence}
              />
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function describeShape(shape: ReadingShape): string {
  switch (shape) {
    case "walk":
      return "Walkthrough";
    case "participantMap":
      return "Participant map";
    case "purposeCards":
      return "Purposes";
    case "participantList":
      return "Participants";
  }
}
