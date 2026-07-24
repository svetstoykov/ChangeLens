import type { ComparisonFreshnessState } from "../Models/ComparisonWorkspaceState";
import type { PreparedComparison } from "../Models/PreparedComparison";

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
      <section aria-labelledby="comparison-summary-heading">
        <h2 id="comparison-summary-heading">Comparison summary</h2>
        <p>Choose a target to prepare a comparison.</p>
      </section>
    );
  }

  return (
    <section aria-labelledby="comparison-summary-heading">
      <h2 id="comparison-summary-heading">Comparison summary</h2>
      <Readiness
        readiness={preparedComparison.readiness.state}
        freshness={freshness}
      />
      <dl>
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
        />
        <Fact
          label="Uncommitted files"
          value={preparedComparison.uncommittedFileTotal}
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
      <p>
        File categories can overlap when one file has both staged and unstaged
        changes.
      </p>
    </section>
  );
}

function Fact({
  label,
  value,
}: {
  readonly label: string;
  readonly value: number;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Readiness({
  readiness,
  freshness,
}: {
  readonly readiness: PreparedComparison["readiness"]["state"];
  readonly freshness: ComparisonFreshnessState;
}) {
  if (freshness === "stale")
    return <p role="status">Comparison is stale. Refresh before analyzing.</p>;
  if (freshness === "unknown")
    return (
      <p role="status">
        Comparison freshness is unknown. Refresh before analyzing.
      </p>
    );
  if (freshness === "checking")
    return <p role="status">Checking comparison freshness…</p>;
  if (readiness === "empty") return <p role="status">No changes to analyze</p>;
  if (readiness === "conflicts")
    return (
      <p role="status">Resolve conflicts outside ChangeLens, then refresh</p>
    );
  return <p role="status">Ready to analyze</p>;
}
