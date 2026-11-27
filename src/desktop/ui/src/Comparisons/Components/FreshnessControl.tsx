import type { ComparisonFreshnessState } from "../Models/ComparisonWorkspaceState";
import { Icon } from "../../Visuals/Components/Icon";

interface FreshnessControlProps {
  readonly freshness: ComparisonFreshnessState;
  readonly hasTarget: boolean;
  readonly isBusy: boolean;
  readonly onRefresh: () => void;
}

export function FreshnessControl({
  freshness,
  hasTarget,
  isBusy,
  onRefresh,
}: FreshnessControlProps) {
  if (!hasTarget) return null;
  const label =
    freshness === "current"
      ? "Current"
      : freshness === "checking"
        ? "Checking"
        : freshness === "stale"
          ? "Stale"
          : "Unknown";
  const statusIcon = freshness === "current" ? "check" : "warning";
  const isUnavailable = freshness === "checking" || isBusy;
  return (
    <section
      className="freshness-control"
      aria-label="Comparison freshness"
      data-freshness={freshness}
    >
      <div className="freshness-copy">
        <span className="freshness-icon">
          <Icon name={freshness === "checking" ? "refresh" : statusIcon} />
        </span>
        <span>
          <small>Comparison data</small>
          <strong>{label}</strong>
        </span>
      </div>
      <button
        className="icon-button"
        type="button"
        aria-label="Refresh comparison"
        onClick={() => {
          if (!isUnavailable) onRefresh();
        }}
        aria-disabled={isUnavailable}
      >
        <Icon name="refresh" />
      </button>
    </section>
  );
}
