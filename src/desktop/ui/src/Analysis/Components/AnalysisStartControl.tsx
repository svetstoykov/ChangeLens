import { useState } from "react";
import { Icon } from "../../Visuals/Components/Icon";
import type { AnalysisStartBlockedReason } from "../Models/AnalysisStartBlockedReason";

const changeContextLimit = 8192;

export interface AnalysisStartControlProps {
  readonly blockedReason: AnalysisStartBlockedReason | null;
  readonly starting: boolean;
  readonly onStart: (changeContext: string | null) => void;
}

export function AnalysisStartControl({
  blockedReason,
  starting,
  onStart,
}: AnalysisStartControlProps) {
  const [changeContext, setChangeContext] = useState("");
  const trimmedChangeContext = changeContext.trim();
  const isBlocked = blockedReason !== null || starting;

  const handleStart = (): void => {
    onStart(trimmedChangeContext.length > 0 ? trimmedChangeContext : null);
  };

  return (
    <section
      className="change-context"
      aria-labelledby="analysis-start-heading"
    >
      <header className="setup-card-heading">
        <span className="step-number" aria-hidden="true">
          2
        </span>
        <div>
          <p className="eyebrow">
            Change context <span className="optional-badge">Optional</span>
          </p>
          <h3 id="analysis-start-heading">Tell ChangeLens what matters</h3>
          <p>
            Add the task, acceptance criteria, or implementation notes that
            should stay with this analysis run.
          </p>
        </div>
      </header>
      <label className="field-label" htmlFor="analysis-change-context">
        Context for this change
      </label>
      <textarea
        id="analysis-change-context"
        value={changeContext}
        disabled={isBlocked}
        maxLength={changeContextLimit}
        rows={8}
        placeholder="For example: add repository intake and compare it with main. Preserve local-only behavior and surface conflicts clearly."
        onChange={(event) => setChangeContext(event.target.value)}
      />
      <p className="field-help">
        <Icon name="shield" />
        Context is stored with the run on this device and is not sent anywhere
        in this phase.
      </p>
      <div className="analysis-start-actions">
        <button
          className="primary-button"
          type="button"
          data-starting={starting}
          disabled={isBlocked}
          onClick={handleStart}
        >
          <Icon name={starting ? "refresh" : "compare"} />
          {starting ? "Starting…" : "Start analysis"}
        </button>
        <p className="analysis-start-hint">
          {describeStartHint(blockedReason)}
        </p>
      </div>
    </section>
  );
}

function describeStartHint(
  blockedReason: AnalysisStartBlockedReason | null,
): string {
  switch (blockedReason) {
    case "comparisonNotReady":
      return "Prepare a current comparison before starting an analysis.";
    case "noCommittedChanges":
      return "There are no committed branch changes available for analysis.";
    default:
      return "ChangeLens records an immutable snapshot of the committed branch changes and collects repository facts.";
  }
}
