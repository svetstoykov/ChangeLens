import type { ComparisonFreshnessState } from "../Models/ComparisonWorkspaceState";
import type { ComparisonReadiness } from "../Models/ComparisonReadiness";
import type { PreparedComparison } from "../Models/PreparedComparison";
import { LocalIcon } from "../../Visuals/Components/LocalIcon";
import checkIcon from "../../assets/check.svg";
import conflictIcon from "../../assets/conflict.svg";
import fileIcon from "../../assets/file.svg";
import refreshIcon from "../../assets/refresh.svg";
import warningIcon from "../../assets/warning.svg";

interface ComparisonSummaryProps {
  readonly preparedComparison: PreparedComparison | null;
  readonly freshness: ComparisonFreshnessState;
}

export function ComparisonSummary({
  preparedComparison,
  freshness,
}: ComparisonSummaryProps) {
  if (preparedComparison === null) {
    return (
      <section
        className="comparison-summary"
        aria-labelledby="comparison-summary-heading"
      >
        <h3 id="comparison-summary-heading">Current change</h3>
        <p>Choose a target to prepare a comparison.</p>
      </section>
    );
  }

  return (
    <section
      className="comparison-summary"
      aria-labelledby="comparison-summary-heading"
    >
      <p className="eyebrow">Aggregate facts</p>
      <h3 id="comparison-summary-heading">Current change</h3>
      <Readiness
        readiness={preparedComparison.readiness}
        freshness={freshness}
      />
      <dl className="comparison-facts">
        <Fact
          label="Current work commits"
          value={preparedComparison.currentWorkCommitCount}
        />
        <Fact
          label="Target-only commits"
          value={preparedComparison.targetOnlyCommitCount}
        />
        <Fact
          label="Changed files"
          value={preparedComparison.changedFileTotal}
          icon={fileIcon}
        />
        <Fact
          label="Uncommitted files"
          value={preparedComparison.uncommittedFileTotal}
          icon={warningIcon}
        />
        <Fact label="Staged files" value={preparedComparison.stagedFileCount} />
        <Fact
          label="Unstaged files"
          value={preparedComparison.unstagedFileCount}
        />
        <Fact
          label="Untracked files"
          value={preparedComparison.untrackedFileCount}
        />
        {preparedComparison.readiness.state === "conflicts" ? (
          <Fact
            label="Conflicted files"
            value={preparedComparison.readiness.conflictedFileCount}
            icon={conflictIcon}
          />
        ) : null}
        <div className="technical-fact">
          <dt>Target revision</dt>
          <dd>
            <code>{preparedComparison.target.revision}</code>
          </dd>
        </div>
        <div className="technical-fact">
          <dt>Merge base</dt>
          <dd>
            <code>{preparedComparison.mergeBaseRevision}</code>
          </dd>
        </div>
      </dl>
      <p className="fact-note">
        File categories can overlap when one file has both staged and unstaged
        changes.
      </p>
    </section>
  );
}

function Fact({
  label,
  value,
  icon,
}: {
  readonly label: string;
  readonly value: number;
  readonly icon?: string;
}) {
  return (
    <div>
      <dt>
        {icon ? <LocalIcon source={icon} /> : null}
        {label}
      </dt>
      <dd>{value}</dd>
    </div>
  );
}

function Readiness({
  readiness,
  freshness,
}: {
  readonly readiness: ComparisonReadiness;
  readonly freshness: ComparisonFreshnessState;
}) {
  if (freshness === "stale")
    return (
      <ReadinessStatus
        state="stale"
        icon={warningIcon}
        text="Comparison is stale. Refresh before analyzing."
      />
    );
  if (freshness === "unknown")
    return (
      <ReadinessStatus
        state="unknown"
        icon={warningIcon}
        text="Comparison freshness is unknown. Refresh before analyzing."
      />
    );
  if (freshness === "checking")
    return (
      <ReadinessStatus
        state="checking"
        icon={refreshIcon}
        text="Checking comparison freshness…"
      />
    );
  if (readiness.state === "empty")
    return (
      <ReadinessStatus
        state="empty"
        icon={fileIcon}
        text="No changes to analyze"
      />
    );
  if (readiness.state === "conflicts")
    return (
      <ReadinessStatus
        state="conflicts"
        icon={conflictIcon}
        text="Resolve conflicts outside ChangeLens, then refresh"
      />
    );
  return (
    <ReadinessStatus state="ready" icon={checkIcon} text="Ready to analyze" />
  );
}

function ReadinessStatus({
  state,
  icon,
  text,
}: {
  readonly state: string;
  readonly icon: string;
  readonly text: string;
}) {
  return (
    <p className="readiness-status" data-readiness={state} role="status">
      <LocalIcon source={icon} />
      <span>{text}</span>
    </p>
  );
}
