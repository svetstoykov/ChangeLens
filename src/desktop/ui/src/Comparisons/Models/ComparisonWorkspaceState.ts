import type { ActionError } from "../../Actions/Models/ActionError";
import type { PreparedComparison } from "./PreparedComparison";
import type { ComparisonTarget } from "./ComparisonTarget";

export type ComparisonFreshnessState =
  "current" | "checking" | "stale" | "unknown";

export type RemoteBaselineStatus =
  "idle" | "checking" | "current" | "moved" | "refreshing" | "unreachable";

export interface ComparisonWorkspaceState {
  readonly targets: readonly ComparisonTarget[];
  readonly selectedTarget: ComparisonTarget | null;
  readonly preparedComparison: PreparedComparison | null;
  readonly freshness: ComparisonFreshnessState;
  readonly remoteBaseline: RemoteBaselineStatus;
  readonly remoteBaselineRevision: string | null;
  readonly error: ActionError | null;
  readonly errorSource:
    | "discovery"
    | "preparation"
    | "freshness"
    | "refresh"
    | "remoteBaseline"
    | null;
  readonly query: string;
  readonly nextCursor: string | null;
  readonly targetSetToken: string | null;
  readonly unsupportedTargetCount: number;
  readonly isDiscovering: boolean;
  readonly isPreparing: boolean;
  readonly isRefreshing: boolean;
}
