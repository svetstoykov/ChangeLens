import { useState } from "react";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import { useComparisonController } from "../Hooks/useComparisonController";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { ComparisonSummary } from "./ComparisonSummary";
import { FreshnessControl } from "./FreshnessControl";
import { TargetCombobox } from "./TargetCombobox";
import { Icon } from "../../Visuals/Components/Icon";

interface RepositoryWorkspaceProps {
  readonly repository: RepositoryDescriptor;
  readonly comparisonClient: ComparisonClient;
  readonly onRepositoryRefreshed: (repository: RepositoryDescriptor) => void;
}

export function RepositoryWorkspace({
  repository,
  comparisonClient,
  onRepositoryRefreshed,
}: RepositoryWorkspaceProps) {
  const [changeContext, setChangeContext] = useState("");
  const controller = useComparisonController({
    repository,
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

  return (
    <section
      className="repository-workspace"
      aria-labelledby="repository-workspace-heading"
    >
      <header className="workspace-hero">
        <div className="workspace-heading">
          <p className="eyebrow">
            <Icon name="currentChange" />
            Current change
          </p>
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
                </div>
              </div>
            </section>
          ) : null}
          <section
            className="change-context"
            aria-labelledby="change-context-heading"
          >
            <header className="setup-card-heading">
              <span className="step-number" aria-hidden="true">
                2
              </span>
              <div>
                <p className="eyebrow">
                  Change context{" "}
                  <span className="optional-badge">Optional</span>
                </p>
                <h3 id="change-context-heading">
                  Tell ChangeLens what matters
                </h3>
                <p>
                  Add the task, acceptance criteria, or implementation notes
                  that should stay with this workspace.
                </p>
              </div>
            </header>
            <label className="field-label" htmlFor="change-context">
              Context for this change
            </label>
            <textarea
              id="change-context"
              value={changeContext}
              onChange={(event) => setChangeContext(event.target.value)}
              rows={8}
              placeholder="For example: add repository intake and compare it with main. Preserve local-only behavior and surface conflicts clearly."
            />
            <p className="field-help">
              <Icon name="shield" />
              Context remains local and is not sent anywhere in this phase.
            </p>
          </section>
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
          {state.isRefreshing && !state.isPreparing ? (
            <p className="workspace-progress" role="status">
              <Icon name="refresh" />
              Refreshing comparison…
            </p>
          ) : null}
          <ComparisonSummary
            preparedComparison={state.preparedComparison}
            freshness={state.freshness}
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
