import type { ComparisonFreshnessState } from "../Models/ComparisonWorkspaceState";
import { LocalIcon } from "../../Visuals/Components/LocalIcon";
import checkIcon from "../../assets/check.svg";
import refreshIcon from "../../assets/refresh.svg";
import warningIcon from "../../assets/warning.svg";

interface FreshnessControlProps {
  readonly freshness: ComparisonFreshnessState;
  readonly hasTarget: boolean;
  readonly onRefresh: () => void;
}

export function FreshnessControl({
  freshness,
  hasTarget,
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
  const statusIcon = freshness === "current" ? checkIcon : warningIcon;
  return (
    <section
      className="freshness-control"
      aria-label="Comparison freshness"
      data-freshness={freshness}
    >
      <p>
        <LocalIcon
          source={freshness === "checking" ? refreshIcon : statusIcon}
        />
        <span>Freshness: {label}</span>
      </p>
      <button
        className="icon-button"
        type="button"
        aria-label="Refresh comparison"
        onClick={onRefresh}
        disabled={freshness === "checking"}
      >
        <LocalIcon source={refreshIcon} />
      </button>
    </section>
  );
}
