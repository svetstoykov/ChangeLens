import type { ReadingCitation } from "../Models/ReadingCitation";
import type { ReadingEvidence } from "../Models/ReadingEvidence";
import type { ReadingFinding } from "../Models/ReadingFinding";
import { describeFindingSeverity } from "./describeFindingSeverity";
import { ReadingClaimQuotes } from "./ReadingClaimQuotes";

export interface ReadingFindingItemProps {
  readonly finding: ReadingFinding;
  readonly areaName: string;
  readonly citations: readonly ReadingCitation[];
  readonly evidence: readonly ReadingEvidence[];
}

export function ReadingFindingItem({
  finding,
  areaName,
  citations,
  evidence,
}: ReadingFindingItemProps) {
  return (
    <li className="reading-finding" data-severity={finding.severity}>
      <header className="reading-finding-heading">
        <span className="reading-finding-badge">
          {describeFindingSeverity(finding.severity)}
        </span>
        <h5>{finding.title}</h5>
      </header>
      <p className="reading-finding-area">{areaName}</p>
      <dl className="reading-finding-details">
        <div>
          <dt>Trigger</dt>
          <dd>{finding.trigger}</dd>
        </div>
        <div>
          <dt>Impact</dt>
          <dd>{finding.impact}</dd>
        </div>
        <div>
          <dt>Fix</dt>
          <dd>{finding.fix}</dd>
        </div>
      </dl>
      <ReadingClaimQuotes
        claimId={`finding:${finding.id}`}
        citations={citations}
        evidence={evidence}
      />
    </li>
  );
}
