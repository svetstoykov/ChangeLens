import type { ReactNode } from "react";
import type { ActionError } from "../../Actions/Models/ActionError";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { Icon } from "../../Visuals/Components/Icon";
import type { AnalysisFact } from "../Models/AnalysisFact";
import type { AnalysisRunSummary } from "../Models/AnalysisRunSummary";
import type { AnalysisActiveStage } from "../Models/AnalysisRunState";
import { ANALYSIS_ACTIVE_STAGE_ORDER } from "../Models/AnalysisRunState";
import type { AnalysisTerminalSummary } from "../Models/AnalysisTerminalSummary";

type StageStatus = "complete" | "active" | "pending";

type RunTone = "running" | "verified" | "warning" | "danger" | "unknown";

interface StageCopy {
  readonly label: string;
  readonly description: string;
}

interface RunStatus {
  readonly heading: string;
  readonly detail: string;
  readonly tone: RunTone;
}

export interface AnalysisProgressViewProps {
  readonly summary: AnalysisRunSummary | null;
  readonly error: ActionError | null;
  readonly actionError: ActionError | null;
  readonly attached: boolean;
  readonly onRetry: () => void;
  readonly onCancel: () => void;
  readonly onDismissActionError: () => void;
  readonly onReturnToSetup: () => void;
}

export function AnalysisProgressView({
  summary,
  error,
  actionError,
  attached,
  onRetry,
  onCancel,
  onDismissActionError,
  onReturnToSetup,
}: AnalysisProgressViewProps) {
  if (error !== null) {
    return (
      <section className="analysis-progress" aria-label="Analysis progress">
        <AnalysisActionAlert error={error}>
          <button type="button" onClick={onRetry}>
            <Icon name="refresh" />
            Retry
          </button>
          <button type="button" onClick={onReturnToSetup}>
            <Icon name="currentChange" />
            Back to repository setup
          </button>
        </AnalysisActionAlert>
      </section>
    );
  }

  if (summary === null) {
    return (
      <section className="analysis-progress" aria-label="Analysis progress">
        <p className="workspace-progress" aria-live="polite">
          <Icon name="refresh" />
          Loading analysis run…
        </p>
      </section>
    );
  }

  const isTerminalOrInterrupted =
    summary.terminal !== null || summary.state === "interrupted";
  const currentStageIndex = ANALYSIS_ACTIVE_STAGE_ORDER.findIndex(
    (stage) => stage === summary.state,
  );
  const status = describeRunStatus(summary);

  return (
    <section
      className="analysis-progress"
      aria-labelledby="analysis-progress-heading"
    >
      <header className="analysis-progress-hero">
        <div className="analysis-progress-heading">
          <p className="eyebrow">Analysis run</p>
          <h2 id="analysis-progress-heading">{status.heading}</h2>
          <p className="workspace-description">{status.detail}</p>
        </div>
        <section className="workspace-repository" aria-label="Analysed change">
          <span className="workspace-repository-icon">
            <Icon name="folder" />
          </span>
          <span className="workspace-repository-copy">
            <strong>{summary.repository.displayName}</strong>
            <code title={summary.repository.canonicalPath}>
              {summary.repository.canonicalPath}
            </code>
          </span>
          <span className="workspace-head" title={summary.comparison.target}>
            <Icon name="compare" />
            <code>{summary.comparison.target}</code>
          </span>
        </section>
      </header>
      {attached ? (
        <p className="analysis-attachment-notice" role="status">
          <Icon name="info" />
          This analysis run was already in progress for this repository.
          ChangeLens reattached to it instead of starting a new one.
        </p>
      ) : null}
      {actionError !== null ? (
        <AnalysisActionAlert error={actionError}>
          <button type="button" onClick={onDismissActionError}>
            <Icon name="check" />
            Dismiss
          </button>
        </AnalysisActionAlert>
      ) : null}
      <div className="analysis-progress-layout">
        <section className="analysis-stage-track" aria-label="Analysis stages">
          <ol className="analysis-stages">
            {ANALYSIS_ACTIVE_STAGE_ORDER.map((stage, index) => {
              const stageStatus = describeStageStatus(
                index,
                currentStageIndex,
                isTerminalOrInterrupted,
              );
              const copy = describeStage(stage);
              return (
                <li key={stage} data-status={stageStatus}>
                  <span className="analysis-stage-marker" aria-hidden="true">
                    {stageStatus === "complete" ? (
                      <Icon name="check" />
                    ) : (
                      <span className="analysis-stage-dot" />
                    )}
                  </span>
                  <span className="analysis-stage-copy">
                    <strong>{copy.label}</strong>
                    <code>{copy.description}</code>
                  </span>
                </li>
              );
            })}
          </ol>
          <p
            className="analysis-run-status"
            data-tone={status.tone}
            role="status"
          >
            <Icon name={statusIconName(status.tone)} />
            <span>{status.heading}</span>
          </p>
          <div className="analysis-run-actions">
            {isTerminalOrInterrupted ? (
              <button
                className="primary-button"
                type="button"
                onClick={onReturnToSetup}
              >
                <Icon name="currentChange" />
                Back to repository setup
              </button>
            ) : (
              <button
                type="button"
                disabled={summary.cancellationRequested}
                onClick={onCancel}
              >
                <Icon name="conflict" />
                {summary.cancellationRequested
                  ? "Cancelling…"
                  : "Cancel analysis"}
              </button>
            )}
          </div>
        </section>
        <section className="analysis-fact-rail" aria-label="Discovered so far">
          <header className="summary-heading">
            <span className="summary-heading-icon">
              <Icon name="compare" />
            </span>
            <div>
              <p className="eyebrow">Discovered so far</p>
              <h3>Repository facts</h3>
            </div>
          </header>
          {summary.facts.length === 0 ? (
            <p className="fact-note">
              Facts appear here as ChangeLens reads the captured snapshot.
            </p>
          ) : (
            <dl className="analysis-facts">
              {summary.facts.map((fact) => (
                <div
                  key={fact.kind}
                  data-tone={
                    fact.kind === "excludedUncommittedFiles"
                      ? "warning"
                      : "neutral"
                  }
                >
                  <dt>{describeFactKind(fact)}</dt>
                  <dd>{fact.count}</dd>
                  {fact.detail === null ? null : (
                    <p className="fact-note">{fact.detail}</p>
                  )}
                </div>
              ))}
            </dl>
          )}
        </section>
      </div>
    </section>
  );
}

function AnalysisActionAlert({
  error,
  children,
}: {
  readonly error: ActionError;
  readonly children: ReactNode;
}) {
  const presentation = presentActionError(error);
  return (
    <section className="action-alert" role="alert">
      <Icon name="warning" />
      <div>
        <strong>{presentation.title}</strong>
        <ul>
          {presentation.messages.map((message, index) => (
            <li key={`${error.errors[index]?.code ?? "analysis"}-${index}`}>
              {message}
            </li>
          ))}
        </ul>
        <div className="alert-actions">{children}</div>
      </div>
    </section>
  );
}

function describeStageStatus(
  index: number,
  currentStageIndex: number,
  isTerminalOrInterrupted: boolean,
): StageStatus {
  if (isTerminalOrInterrupted) return "complete";
  if (index < currentStageIndex) return "complete";
  return index === currentStageIndex ? "active" : "pending";
}

function describeStage(stage: AnalysisActiveStage): StageCopy {
  switch (stage) {
    case "capturing":
      return {
        label: "Capturing the change",
        description: "Recording an immutable snapshot of this comparison.",
      };
    case "discovering":
      return {
        label: "Discovering repository structure",
        description: "Reading manifests and repository layout.",
      };
    case "collecting":
      return {
        label: "Collecting evidence",
        description: "Gathering source-control and syntactic facts.",
      };
    case "persisting":
      return {
        label: "Persisting the run",
        description: "Writing durable local run artifacts.",
      };
  }
}

function describeRunStatus(summary: AnalysisRunSummary): RunStatus {
  if (summary.state === "interrupted") {
    return {
      heading: "Analysis interrupted",
      detail:
        "An earlier engine session stopped before this run reached a result.",
      tone: "unknown",
    };
  }

  if (summary.terminal === null) {
    return summary.cancellationRequested
      ? {
          heading: "Cancelling analysis",
          detail: "Waiting for the engine to stop this run at a safe point.",
          tone: "warning",
        }
      : {
          heading: "Analyzing current change",
          detail:
            "ChangeLens is reading the captured snapshot for this comparison.",
          tone: "running",
        };
  }

  return describeTerminalStatus(summary.terminal);
}

function describeTerminalStatus(terminal: AnalysisTerminalSummary): RunStatus {
  switch (terminal.kind) {
    case "completed":
      return {
        heading: "Analysis complete",
        detail: "Every stage finished and the run is stored locally.",
        tone: "verified",
      };
    case "completedWithLimitations":
      return {
        heading: "Analysis complete with limitations",
        detail: `${terminal.limitationCount} ${terminal.limitationCount === 1 ? "limitation" : "limitations"} were recorded while collecting evidence.`,
        tone: "warning",
      };
    case "cancelled":
      return {
        heading: "Analysis cancelled",
        detail: "The run stopped at your request and kept no partial result.",
        tone: "unknown",
      };
    case "failed":
      return {
        heading: "Analysis failed",
        detail: `The engine reported ${terminal.failureCode}.`,
        tone: "danger",
      };
  }
}

function statusIconName(
  tone: RunTone,
): "refresh" | "check" | "warning" | "conflict" | "info" {
  switch (tone) {
    case "running":
      return "refresh";
    case "verified":
      return "check";
    case "warning":
      return "warning";
    case "danger":
      return "conflict";
    case "unknown":
      return "info";
  }
}

function describeFactKind(fact: AnalysisFact): string {
  return fact.kind
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/^./, (character) => character.toUpperCase());
}
