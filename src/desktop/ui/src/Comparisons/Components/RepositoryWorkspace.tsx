import { AnalysisStartControl } from "../../Analysis/Components/AnalysisStartControl";
import type { AnalysisStartRequest } from "../../Analysis/Models/AnalysisStartRequest";
import type { AnalysisStartOutcome } from "../../Analysis/Models/AnalysisStartOutcome";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import { useComparisonController } from "../Hooks/useComparisonController";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import type { ActionError } from "../../Actions/Models/ActionError";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { ComparisonSummary } from "./ComparisonSummary";
import { FreshnessControl } from "./FreshnessControl";
import { TargetCombobox } from "./TargetCombobox";
import { Icon } from "../../Visuals/Components/Icon";
import type { AnalysisStartBlockedReason } from "../../Analysis/Models/AnalysisStartBlockedReason";

interface RepositoryWorkspaceProps {
  readonly repository: RepositoryDescriptor;
  readonly preferredTarget: string | null;
  readonly comparisonClient: ComparisonClient;
  readonly startingAnalysis: boolean;
  readonly analysisError: ActionError | null;
  readonly onRepositoryRefreshed: (repository: RepositoryDescriptor) => void;
  readonly onStartAnalysis: (
    request: AnalysisStartRequest,
  ) => Promise<AnalysisStartOutcome>;
  readonly onDismissAnalysisError: () => void;
}

export function RepositoryWorkspace({
  repository,
  preferredTarget,
  comparisonClient,
  startingAnalysis,
  analysisError,
  onRepositoryRefreshed,
  onStartAnalysis,
  onDismissAnalysisError,
}: RepositoryWorkspaceProps) {
  const controller = useComparisonController({
    repository,
    preferredTarget,
    comparisonClient,
    onRepositoryRefreshed,
  });
  const { state } = controller;
  const error = state.error ? presentActionError(state.error) : null;
  const errorCode = state.error?.errors[0].code;
  const canRetryError =
    errorCode === "comparison.timedOut" ||
    errorCode === "comparison.inspectionFailed";
  const headName =
    repository.head.kind === "branch" ? repository.head.name : "Detached HEAD";
  const analysisErrorPresentation =
    analysisError !== null ? presentActionError(analysisError) : null;
  const preparedComparison = state.preparedComparison;
  const startBlockedReason: AnalysisStartBlockedReason | null =
    preparedComparison === null ||
    preparedComparison.readiness.state !== "ready" ||
    state.freshness !== "current" ||
    state.isPreparing ||
    state.isRefreshing
      ? "comparisonNotReady"
      : preparedComparison.currentWorkCommitCount === 0
        ? "noCommittedChanges"
        : null;

  async function startAnalysis(changeContext: string | null): Promise<void> {
    if (preparedComparison === null) return;
    const outcome = await onStartAnalysis({
      target: preparedComparison.target.fullName,
      freshnessToken: preparedComparison.freshnessToken,
      changeContext,
    });
    if (outcome === "stale") controller.refresh();
  }

  return (
    <section
      className="repository-workspace"
      aria-labelledby="repository-workspace-heading"
    >
      <header className="workspace-hero">
        <div className="workspace-heading">
          <h2 id="repository-workspace-heading">Prepare your comparison</h2>
          <p className="workspace-description">
            Choose a baseline and add context before ChangeLens analyzes the
            work in this repository.
          </p>
        </div>
        <section className="workspace-repository" aria-label="Repository">
          <span className="workspace-repository-icon">
            <Icon name="folder" />
          </span>
          <span className="workspace-repository-copy">
            <strong>{repository.name}</strong>
            <code title={repository.canonicalPath}>
              {repository.canonicalPath}
            </code>
          </span>
          <span className="workspace-head" title={headName}>
            <Icon
              name={repository.head.kind === "branch" ? "branch" : "detached"}
            />
            <code>{headName}</code>
          </span>
        </section>
      </header>
      <div className="comparison-layout">
        <section className="comparison-setup" aria-label="Comparison setup">
          <TargetCombobox
            targets={state.targets}
            selectedTarget={state.selectedTarget}
            query={state.query}
            nextCursor={state.nextCursor}
            unsupportedTargetCount={state.unsupportedTargetCount}
            isDiscovering={state.isDiscovering}
            onQueryChange={controller.setQuery}
            onSelect={controller.selectTarget}
            onLoadMore={controller.loadMore}
          />
          {error ? (
            <section className="action-alert" role="alert">
              <Icon name="warning" />
              <div>
                <strong>{error.title}</strong>
                <ul>
                  {error.messages.map((message, index) => (
                    <li
                      key={`${state.error?.errors[index]?.code ?? "comparison"}-${index}`}
                    >
                      {message}
                    </li>
                  ))}
                </ul>
                <div className="alert-actions">
                  {errorCode === "comparison.invalidTargetQuery" ? (
                    <button type="button" onClick={controller.resetSearch}>
                      Reset search
                    </button>
                  ) : null}
                  {canRetryError && state.errorSource === "discovery" ? (
                    <button type="button" onClick={controller.retryDiscovery}>
                      Retry loading targets
                    </button>
                  ) : null}
                  {canRetryError && state.errorSource === "preparation" ? (
                    <button type="button" onClick={controller.retryPreparation}>
                      Retry preparation
                    </button>
                  ) : null}
                  {canRetryError && state.errorSource === "refresh" ? (
                    <button type="button" onClick={controller.refresh}>
                      Retry refresh
                    </button>
                  ) : null}
                  {canRetryError && state.errorSource === "remoteBaseline" ? (
                    <button
                      type="button"
                      onClick={controller.refreshRemoteBaseline}
                    >
                      Retry refresh
                    </button>
                  ) : null}
                </div>
              </div>
            </section>
          ) : null}
          {analysisErrorPresentation ? (
            <section className="action-alert" role="alert">
              <Icon name="warning" />
              <div>
                <strong>{analysisErrorPresentation.title}</strong>
                <ul>
                  {analysisErrorPresentation.messages.map((message, index) => (
                    <li
                      key={`${analysisError?.errors[index]?.code ?? "analysis"}-${index}`}
                    >
                      {message}
                    </li>
                  ))}
                </ul>
                <div className="alert-actions">
                  <button type="button" onClick={onDismissAnalysisError}>
                    Dismiss
                  </button>
                </div>
              </div>
            </section>
          ) : null}
          <AnalysisStartControl
            blockedReason={startBlockedReason}
            starting={startingAnalysis}
            onStart={(changeContext) => void startAnalysis(changeContext)}
          />
        </section>
        <section
          className="current-change-facts"
          aria-label="Current change facts"
        >
          {state.isPreparing ? (
            <p className="workspace-progress" role="status">
              <Icon name="refresh" />
              Preparing comparison…
            </p>
          ) : null}
          {state.isRefreshing &&
          !state.isPreparing &&
          state.remoteBaseline !== "refreshing" ? (
            <p className="workspace-progress" role="status">
              <Icon name="refresh" />
              Refreshing comparison…
            </p>
          ) : null}
          <ComparisonSummary
            preparedComparison={state.preparedComparison}
            freshness={state.freshness}
            remoteBaseline={state.remoteBaseline}
            onRefreshRemoteBaseline={controller.refreshRemoteBaseline}
            onCancelRemoteBaselineRefresh={
              controller.cancelRemoteBaselineRefresh
            }
          />
          <FreshnessControl
            freshness={state.freshness}
            hasTarget={state.selectedTarget !== null}
            isBusy={state.isPreparing || state.isRefreshing}
            onRefresh={controller.refresh}
          />
        </section>
      </div>
    </section>
  );
}
