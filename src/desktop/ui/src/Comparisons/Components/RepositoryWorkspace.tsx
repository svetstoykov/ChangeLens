import { useState } from "react";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import { useComparisonController } from "../Hooks/useComparisonController";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { ComparisonSummary } from "./ComparisonSummary";
import { FreshnessControl } from "./FreshnessControl";
import { TargetCombobox } from "./TargetCombobox";

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
    <section aria-label="Repository workspace">
      <section aria-labelledby="repository-workspace-heading">
        <h2 id="repository-workspace-heading">{repository.name}</h2>
        <dl>
          <div>
            <dt>Repository path</dt>
            <dd>
              <code>{repository.canonicalPath}</code>
            </dd>
          </div>
          <div>
            <dt>HEAD</dt>
            <dd>
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
      </section>
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
        <section role="alert">
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
          {state.error?.errors[0].code === "comparison.invalidTargetQuery" ? (
            <button type="button" onClick={controller.resetSearch}>
              Reset search
            </button>
          ) : null}
        </section>
      ) : null}
      <ComparisonSummary
        preparedComparison={state.preparedComparison}
        freshness={state.freshness}
      />
      <FreshnessControl
        freshness={state.freshness}
        hasTarget={state.selectedTarget !== null}
        onRefresh={controller.refresh}
      />
      <section aria-labelledby="change-context-heading">
        <h2 id="change-context-heading">Change context</h2>
        <label htmlFor="change-context">
          Describe the change you want to understand (optional)
        </label>
        <textarea
          id="change-context"
          value={changeContext}
          onChange={(event) => setChangeContext(event.target.value)}
        />
      </section>
    </section>
  );
}
