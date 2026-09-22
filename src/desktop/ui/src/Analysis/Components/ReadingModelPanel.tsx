import type { ReadingLimitationKind } from "../Models/ReadingLimitationKind";
import type { ReadingModel } from "../Models/ReadingModel";
import { ReadingAreaSection } from "./ReadingAreaSection";
import { ReadingClaimQuotes } from "./ReadingClaimQuotes";

export interface ReadingModelPanelProps {
  readonly model: ReadingModel;
}

export function ReadingModelPanel({ model }: ReadingModelPanelProps) {
  return (
    <section className="reading-panel" aria-labelledby="reading-model-heading">
      <header className="reading-panel-heading">
        <p className="eyebrow">Reading model</p>
        <h2 id="reading-model-heading">What ChangeLens read</h2>
      </header>
      {model.thesis !== null ? (
        <div className="reading-thesis">
          <p className="reading-statement-text">{model.thesis.text}</p>
          <ReadingClaimQuotes
            claimId={model.thesis.claimId}
            citations={model.citations}
            evidence={model.evidence}
          />
        </div>
      ) : null}
      {model.areas.map((area) => (
        <ReadingAreaSection
          key={area.id}
          area={area}
          citations={model.citations}
          evidence={model.evidence}
        />
      ))}
      {model.limitations.length > 0 ? (
        <section
          className="reading-limitations"
          aria-labelledby="reading-limitations-heading"
        >
          <h3 id="reading-limitations-heading">Limitations</h3>
          <ul>
            {model.limitations.map((limitation, index) => (
              <li
                key={`${limitation.kind}:${limitation.path ?? "run"}:${index}`}
                className="reading-limitation"
              >
                <span className="reading-limitation-kind">
                  {describeLimitationKind(limitation.kind)}
                </span>
                {limitation.path !== null ? (
                  <code className="reading-limitation-path">
                    {limitation.path}
                  </code>
                ) : null}
                <p className="reading-limitation-detail">{limitation.detail}</p>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      {model.omissionSummaries.length > 0 ? (
        <section
          className="reading-omissions"
          aria-labelledby="reading-omissions-heading"
        >
          <h3 id="reading-omissions-heading">Omission summaries</h3>
          <ul>
            {model.omissionSummaries.map((summary) => {
              const matchingPaths = model.limitations.flatMap((limitation) =>
                limitation.kind === summary.sourceKind &&
                limitation.path !== null
                  ? [limitation.path]
                  : [],
              );
              return (
                <li key={summary.sourceKind} className="reading-omission">
                  <dl className="reading-omission-counts">
                    <div>
                      <dt>Total</dt>
                      <dd>{summary.totalCount}</dd>
                    </div>
                    <div>
                      <dt>Sampled</dt>
                      <dd>{summary.sampleCount}</dd>
                    </div>
                    <div>
                      <dt>Resolved</dt>
                      <dd>{summary.resolvedSampleCount}</dd>
                    </div>
                  </dl>
                  <p className="reading-omission-reason">{summary.reason}</p>
                  {matchingPaths.length > 0 ? (
                    <ul className="reading-omission-paths">
                      {matchingPaths.map((path) => (
                        <li key={path}>
                          <code>{path}</code>
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </section>
      ) : null}
      {model.assurances.length > 0 ? (
        <section
          className="reading-assurances"
          aria-labelledby="reading-assurances-heading"
        >
          <h3 id="reading-assurances-heading">Assurances</h3>
          <ul>
            {model.assurances.map((assurance, index) => (
              <li
                key={`${assurance.kind}:${assurance.claimId ?? "run"}:${index}`}
              >
                {assurance.detail}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </section>
  );
}

function describeLimitationKind(kind: ReadingLimitationKind): string {
  switch (kind) {
    case "fileNotRead":
      return "File not read";
    case "fileNotQuoted":
      return "File not quoted";
    case "uncommittedWorkExcluded":
      return "Uncommitted work excluded";
  }
}
