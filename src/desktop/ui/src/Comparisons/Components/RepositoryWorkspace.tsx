import { useState } from "react";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import { useComparisonController } from "../Hooks/useComparisonController";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { ComparisonSummary } from "./ComparisonSummary";
import { FreshnessControl } from "./FreshnessControl";
import { TargetCombobox } from "./TargetCombobox";
import { LocalIcon } from "../../Visuals/Components/LocalIcon";
import branchIcon from "../../assets/branch.svg";
import detachedIcon from "../../assets/detached.svg";
import folderIcon from "../../assets/folder.svg";

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

  return (
    <section
      className="repository-workspace"
      aria-labelledby="repository-workspace-heading"
    >
      <header className="workspace-heading">
        <p className="eyebrow">Current change</p>
        <h2 id="repository-workspace-heading">Prepare the comparison</h2>
        <p className="workspace-repository-name">
          <LocalIcon source={folderIcon} />
          <span>{repository.name}</span>
        </p>
        <dl className="workspace-repository-facts">
          <div>
            <dt>Repository path</dt>
            <dd>
              <code>{repository.canonicalPath}</code>
            </dd>
          </div>
          <div>
            <dt>HEAD</dt>
            <dd className="workspace-head">
              <LocalIcon
                source={
                  repository.head.kind === "branch" ? branchIcon : detachedIcon
                }
              />
              <code>
                {repository.head.kind === "branch"
                  ? repository.head.name
                  : "Detached HEAD"}
              </code>
            </dd>
          </div>
          <div>
            <dt>Revision</dt>
            <dd>
              <code>{repository.head.revision}</code>
            </dd>
          </div>
        </dl>
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
              {state.error?.errors[0].code ===
              "comparison.invalidTargetQuery" ? (
                <button type="button" onClick={controller.resetSearch}>
                  Reset search
                </button>
              ) : null}
            </section>
          ) : null}
          <section
            className="change-context"
            aria-labelledby="change-context-heading"
          >
            <h3 id="change-context-heading">Change context</h3>
            <p>
              Add task details, acceptance criteria, or implementation notes
              that should remain with this workspace.
            </p>
            <label htmlFor="change-context">
              Describe the change you want to understand (optional)
            </label>
            <textarea
              id="change-context"
              value={changeContext}
              onChange={(event) => setChangeContext(event.target.value)}
              rows={8}
              placeholder="Add local context for this change…"
            />
          </section>
        </section>
        <section
          className="current-change-facts"
          aria-label="Current change facts"
        >
          <ComparisonSummary
            preparedComparison={state.preparedComparison}
            freshness={state.freshness}
          />
          <FreshnessControl
            freshness={state.freshness}
            hasTarget={state.selectedTarget !== null}
            onRefresh={controller.refresh}
          />
        </section>
      </div>
    </section>
  );
}
