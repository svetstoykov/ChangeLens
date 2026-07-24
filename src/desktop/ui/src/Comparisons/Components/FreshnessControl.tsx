import type { ComparisonFreshnessState } from "../Models/ComparisonWorkspaceState";

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
  return (
    <section aria-label="Comparison freshness">
      <p>Freshness: {label}</p>
      <button
        type="button"
        onClick={onRefresh}
        disabled={freshness === "checking"}
      >
        Refresh comparison
      </button>
    </section>
  );
}
