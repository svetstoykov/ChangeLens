import type { ValidationRemoval } from "../Models/ValidationRemoval";
import type { ValidationRemovalScope } from "../Models/ValidationRemovalScope";

export interface ValidationRemovalListProps {
  readonly removals: readonly ValidationRemoval[];
}

export function ValidationRemovalList({
  removals,
}: ValidationRemovalListProps) {
  return (
    <section
      className="validation-removals"
      aria-labelledby="validation-removals-heading"
    >
      <header className="validation-removals-heading">
        <p className="eyebrow">Validation</p>
        <h3 id="validation-removals-heading">Removed draft items</h3>
      </header>
      {removals.length === 0 ? (
        <p className="fact-note">No draft items were removed.</p>
      ) : (
        <ul className="validation-removal-list">
          {removals.map((removal, index) => (
            <li key={`${removal.scope}:${removal.id}:${index}`}>
              <span className="validation-removal-scope">
                {describeScope(removal.scope)}
              </span>
              <code className="validation-removal-id">{removal.id}</code>
              <p className="validation-removal-reason">{removal.reason}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function describeScope(scope: ValidationRemovalScope): string {
  switch (scope) {
    case "track":
      return "Track";
    case "participant":
      return "Participant";
    case "relationship":
      return "Relationship";
    case "statement":
      return "Statement";
  }
}
