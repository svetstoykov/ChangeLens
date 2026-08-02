import type { IconName } from "../../Visuals/Components/Icon";
import { Icon } from "../../Visuals/Components/Icon";
import type { ComparisonReadiness } from "../Models/ComparisonReadiness";
import type {
  ComparisonFreshnessState,
  RemoteBaselineStatus,
} from "../Models/ComparisonWorkspaceState";
import type { PreparedComparison } from "../Models/PreparedComparison";

interface ComparisonSummaryProps {
  readonly preparedComparison: PreparedComparison | null;
  readonly freshness: ComparisonFreshnessState;
  readonly remoteBaseline: RemoteBaselineStatus;
  readonly onRefreshRemoteBaseline: () => void;
  readonly onCancelRemoteBaselineRefresh: () => void;
}

export function ComparisonSummary({
  preparedComparison,
  freshness,
  remoteBaseline,
  onRefreshRemoteBaseline,
  onCancelRemoteBaselineRefresh,
}: ComparisonSummaryProps) {
  if (preparedComparison === null) {
    return (
      <section
        className="comparison-summary comparison-summary-empty"
        aria-labelledby="comparison-summary-heading"
      >
        <SummaryHeading />
        <div className="empty-comparison">
          <span>
            <Icon name="compare" />
          </span>
          <strong>No comparison prepared yet</strong>
          <p>
            Select a baseline branch to see change counts, readiness, and
            freshness here.
          </p>
        </div>
      </section>
    );
  }

  return (
    <section
      className="comparison-summary"
      aria-labelledby="comparison-summary-heading"
    >
      <SummaryHeading />
      {preparedComparison.target.kind === "remoteTracking" ? (
        <RemoteBaselineIndicator
          name={preparedComparison.target.name}
          status={remoteBaseline}
          onRefresh={onRefreshRemoteBaseline}
          onCancel={onCancelRemoteBaselineRefresh}
        />
      ) : null}
      <Readiness
        readiness={preparedComparison.readiness}
        freshness={freshness}
      />
      {preparedComparison.uncommittedFileTotal > 0 ? (
        <div className="exclusion-notice" role="status">
          <Icon name="warning" />
          <div className="exclusion-notice-body">
            <strong>
              {preparedComparison.uncommittedFileTotal}{" "}
              {preparedComparison.uncommittedFileTotal === 1 ? "file" : "files"}{" "}
              will not be analyzed
            </strong>
            <small>
              ChangeLens analyzes committed branch changes only. Staged,
              unstaged, and untracked work is excluded from the snapshot.
            </small>
            <small>
              {preparedComparison.stagedFileCount} staged ·{" "}
              {preparedComparison.unstagedFileCount} unstaged ·{" "}
              {preparedComparison.untrackedFileCount} untracked
            </small>
          </div>
        </div>
      ) : null}
      <dl className="comparison-facts">
        <Fact
          label="Changed files"
          value={preparedComparison.changedFileTotal}
          icon="file"
        />
        <Fact
          label="Current commits"
          value={preparedComparison.currentWorkCommitCount}
          icon="commit"
        />
        <Fact
          label="Target-only commits"
          value={preparedComparison.targetOnlyCommitCount}
          icon="branch"
        />
        <Fact
          label="Uncommitted"
          value={preparedComparison.uncommittedFileTotal}
          icon="warning"
          emphasis={preparedComparison.uncommittedFileTotal > 0}
        />
        <Fact
          label="Staged"
          value={preparedComparison.stagedFileCount}
          icon="check"
        />
        <Fact
          label="Unstaged"
          value={preparedComparison.unstagedFileCount}
          icon="file"
        />
        <Fact
          label="Untracked"
          value={preparedComparison.untrackedFileCount}
          icon="info"
        />
        {preparedComparison.readiness.state === "conflicts" ? (
          <Fact
            label="Conflicted"
            value={preparedComparison.readiness.conflictedFileCount}
            icon="conflict"
            emphasis
          />
        ) : null}
      </dl>
      <p className="fact-note">
        File categories can overlap when the same file has staged and unstaged
        changes.
      </p>
      <details className="technical-details">
        <summary>
          <span>Technical details</span>
          <Icon name="chevronDown" />
        </summary>
        <dl>
          <div>
            <dt>Target revision</dt>
            <dd>
              <code>{preparedComparison.target.revision}</code>
            </dd>
          </div>
          <div>
            <dt>Merge base</dt>
            <dd>
              <code>{preparedComparison.mergeBaseRevision}</code>
            </dd>
          </div>
        </dl>
      </details>
    </section>
  );
}

function SummaryHeading() {
  return (
    <header className="summary-heading">
      <span className="summary-heading-icon">
        <Icon name="currentChange" />
      </span>
      <div>
        <p className="eyebrow">Comparison overview</p>
        <h3 id="comparison-summary-heading">Current change</h3>
      </div>
    </header>
  );
}

function Fact({
  label,
  value,
  icon,
  emphasis = false,
}: {
  readonly label: string;
  readonly value: number;
  readonly icon: IconName;
  readonly emphasis?: boolean;
}) {
  return (
    <div data-emphasis={emphasis}>
      <dt>
        <Icon name={icon} />
        {label}
      </dt>
      <dd>{value}</dd>
    </div>
  );
}

function RemoteBaselineIndicator({
  name,
  status,
  onRefresh,
  onCancel,
}: {
  readonly name: string;
  readonly status: RemoteBaselineStatus;
  readonly onRefresh: () => void;
  readonly onCancel: () => void;
}) {
  if (status === "idle" || status === "current") return null;

  const presentation = describeRemoteBaselineStatus(status);

  return (
    <div className="remote-baseline-notice" data-status={status} role="status">
      <Icon name={presentation.icon} />
      <div className="remote-baseline-notice-body">
        <strong>{presentation.title}</strong>
        <small>
          <code>{name}</code> {presentation.detail}
        </small>
      </div>
      {status === "moved" ? (
        <button
          type="button"
          className="remote-baseline-notice-action"
          onClick={onRefresh}
        >
          <Icon name="refresh" />
          Fetch
        </button>
      ) : null}
      {status === "refreshing" ? (
        <button
          type="button"
          className="remote-baseline-notice-action"
          onClick={onCancel}
        >
          Cancel
        </button>
      ) : null}
    </div>
  );
}

function describeRemoteBaselineStatus(
  status: Exclude<RemoteBaselineStatus, "idle" | "current">,
): {
  readonly icon: IconName;
  readonly title: string;
  readonly detail: string;
} {
  switch (status) {
    case "checking":
      return {
        icon: "refresh",
        title: "Checking the remote baseline",
        detail: "is being compared with its remote.",
      };
    case "moved":
      return {
        icon: "warning",
        title: "Remote baseline moved",
        detail: "has new commits that are not fetched yet.",
      };
    case "refreshing":
      return {
        icon: "refresh",
        title: "Fetching the remote baseline",
        detail: "is being updated from its remote.",
      };
    default:
      return {
        icon: "warning",
        title: "Remote baseline check failed",
        detail: "could not be checked, so it may be out of date.",
      };
  }
}

function Readiness({
  readiness,
  freshness,
}: {
  readonly readiness: ComparisonReadiness;
  readonly freshness: ComparisonFreshnessState;
}) {
  if (freshness === "stale") {
    return (
      <ReadinessStatus
        state="stale"
        icon="warning"
        title="Comparison is stale"
        text="Refresh before relying on these facts."
      />
    );
  }
  if (freshness === "unknown") {
    return (
      <ReadinessStatus
        state="unknown"
        icon="warning"
        title="Freshness is unknown"
        text="Refresh before relying on these facts."
      />
    );
  }
  if (freshness === "checking") {
    return (
      <ReadinessStatus
        state="checking"
        icon="refresh"
        title="Checking freshness"
        text="Confirming that the repository still matches."
      />
    );
  }
  if (readiness.state === "empty") {
    return (
      <ReadinessStatus
        state="empty"
        icon="info"
        title="No changes to analyze"
        text="The current work matches the selected baseline."
      />
    );
  }
  if (readiness.state === "conflicts") {
    return (
      <ReadinessStatus
        state="conflicts"
        icon="conflict"
        title="Conflicts need attention"
        text="Resolve them outside ChangeLens, then refresh."
      />
    );
  }
  return (
    <ReadinessStatus
      state="ready"
      icon="check"
      title="Ready to analyze"
      text="The comparison is current and conflict-free."
    />
  );
}

function ReadinessStatus({
  state,
  icon,
  title,
  text,
}: {
  readonly state: string;
  readonly icon: IconName;
  readonly title: string;
  readonly text: string;
}) {
  return (
    <div className="readiness-status" data-readiness={state} role="status">
      <Icon name={icon} />
      <span>
        <strong>{title}</strong>
        <small>{text}</small>
      </span>
    </div>
  );
}
