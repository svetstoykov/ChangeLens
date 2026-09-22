import type { ReadingCitation } from "../Models/ReadingCitation";
import type { ReadingEvidence } from "../Models/ReadingEvidence";
import { splitQuoteByFocus } from "./splitQuoteByFocus";

export interface ReadingClaimQuotesProps {
  readonly claimId: string;
  readonly citations: readonly ReadingCitation[];
  readonly evidence: readonly ReadingEvidence[];
}

export function ReadingClaimQuotes({
  claimId,
  citations,
  evidence,
}: ReadingClaimQuotesProps) {
  const evidenceByNodeId = new Map(
    evidence.map((item) => [item.nodeId, item] as const),
  );
  const quotes = citations.flatMap((citation) => {
    if (citation.claimId !== claimId) return [];
    const item = evidenceByNodeId.get(citation.nodeId);
    if (item === undefined) return [];
    return [{ citation, evidence: item }];
  });

  if (quotes.length === 0) return null;

  return (
    <ul className="reading-quotes">
      {quotes.map(({ citation, evidence: item }) => {
        const segments = splitQuoteByFocus(
          item.text,
          item.startLine,
          item.nodeId,
          citation.focus,
        );
        return (
          <li
            key={`${citation.claimId}:${citation.nodeId}:${item.path}`}
            className="reading-quote"
          >
            <div className="reading-quote-meta">
              <code className="reading-quote-path">{item.path}</code>
              <span className="reading-quote-lines">
                {item.startLine}–{item.endLine}
              </span>
            </div>
            <pre className="reading-quote-text">
              {segments.map((segment) =>
                segment.focused ? (
                  <mark key={segment.key}>{segment.text}</mark>
                ) : (
                  <span key={segment.key}>{segment.text}</span>
                ),
              )}
            </pre>
          </li>
        );
      })}
    </ul>
  );
}
