import type { ActionError } from "../../Actions/Models/ActionError";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { Icon } from "../../Visuals/Components/Icon";
import type { RecentRepository } from "../Models/RecentRepository";

interface RepositoriesDestinationProps {
  readonly repositories: readonly RecentRepository[];
  readonly currentRepositoryId: string | null;
  readonly error: ActionError | null;
  readonly busyRepositoryId: string | null;
  readonly onOpen: (repository: RecentRepository) => void;
  readonly onRemove: (repository: RecentRepository) => void;
  readonly onChooseAnother: () => void;
}

export function RepositoriesDestination({
  repositories,
  currentRepositoryId,
  error,
  busyRepositoryId,
  onOpen,
  onRemove,
  onChooseAnother,
}: RepositoriesDestinationProps) {
  const presentation = error ? presentActionError(error) : null;

  return (
    <section
      className="repositories-destination"
      aria-labelledby="repositories-heading"
    >
      <header className="workspace-hero">
        <div className="workspace-heading">
          <p className="eyebrow">Local repository history</p>
          <h2 id="repositories-heading">Repositories</h2>
          <p className="workspace-description">
            Reopen recent worktrees or remove ChangeLens metadata. Repository
            files are never changed.
          </p>
        </div>
        <button
          className="primary-button"
          type="button"
          onClick={onChooseAnother}
        >
          <Icon name="folder" />
          Choose another repository
        </button>
      </header>
      {presentation ? (
        <section className="action-alert" role="alert">
          <Icon name="warning" />
          <div>
            <strong>{presentation.title}</strong>
            <ul>
              {presentation.messages.map((message, index) => (
                <li key={`${error?.errors[index]?.code ?? "history"}-${index}`}>
                  {message}
                </li>
              ))}
            </ul>
          </div>
        </section>
      ) : null}
      {repositories.length === 0 ? (
        <section className="application-state">
          <Icon name="folder" />
          <h3>No recent repositories</h3>
          <p>Choose a local Git worktree to add it to ChangeLens history.</p>
        </section>
      ) : (
        <ul className="repository-history-list">
          {repositories.map((repository) => {
            const isCurrent = repository.repositoryId === currentRepositoryId;
            const isBusy = busyRepositoryId === repository.repositoryId;
            return (
              <li key={repository.repositoryId}>
                <div>
                  <strong>{repository.name}</strong>
                  {isCurrent ? (
                    <span className="verified-badge">Open this session</span>
                  ) : null}
                  <code title={repository.canonicalPath}>
                    {repository.canonicalPath}
                  </code>
                  <small>
                    Last opened{" "}
                    {new Date(
                      repository.lastOpenedAtUnixMilliseconds,
                    ).toLocaleString()}
                  </small>
                  {repository.preferredTarget ? (
                    <code>Preferred target: {repository.preferredTarget}</code>
                  ) : null}
                </div>
                <div className="repository-history-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={isBusy}
                    onClick={() => onOpen(repository)}
                  >
                    Open
                  </button>
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={isBusy}
                    onClick={() => onRemove(repository)}
                  >
                    Remove
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
