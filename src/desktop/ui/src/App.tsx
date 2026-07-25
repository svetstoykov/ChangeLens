import { useCallback, useEffect, useState } from "react";
import { AppShell } from "./AppShell";
import { RepositoryWorkspace } from "./Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "./Comparisons/Interfaces/ComparisonClient";
import type { ActionError } from "./Actions/Models/ActionError";
import { normalizeActionError } from "./Actions/Services/normalizeActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
import type { EngineStatusClient } from "./EngineStatus/Interfaces/EngineStatusClient";
import { RepositoryIdentity } from "./Repositories/Components/RepositoryIdentity";
import { RepositoryPickerDialog } from "./Repositories/Components/RepositoryPickerDialog";
import { repositoryErrorTitles } from "./Repositories/Constants/repositoryErrorTitles";
import type { RepositoryClient } from "./Repositories/Interfaces/RepositoryClient";
import type { RepositoryFolderPicker } from "./Repositories/Interfaces/RepositoryFolderPicker";
import type { RepositoryDescriptor } from "./Repositories/Models/RepositoryDescriptor";
import { Icon } from "./Visuals/Components/Icon";
import "./styles.css";

interface AppProps {
  readonly engineStatusClient: EngineStatusClient;
  readonly repositoryClient: RepositoryClient;
  readonly repositoryFolderPicker: RepositoryFolderPicker;
  readonly comparisonClient?: ComparisonClient;
}

export type ApplicationState =
  | { readonly status: "checkingEngine" }
  | { readonly status: "engineError"; readonly error: ActionError }
  | { readonly status: "selectingRepository" }
  | {
      readonly status: "repositoryOpen";
      readonly repository: RepositoryDescriptor;
      readonly repositoryGeneration: number;
    }
  | {
      readonly status: "replacingRepository";
      readonly repository: RepositoryDescriptor;
      readonly repositoryGeneration: number;
    };

export function App({
  engineStatusClient,
  repositoryClient,
  repositoryFolderPicker,
  comparisonClient,
}: AppProps) {
  const [state, setState] = useState<ApplicationState>({
    status: "checkingEngine",
  });
  const [retryGeneration, setRetryGeneration] = useState(0);

  useEffect(() => {
    let isCurrent = true;
    engineStatusClient.checkStatus().then(
      () => {
        if (isCurrent) setState({ status: "selectingRepository" });
      },
      (reason: unknown) => {
        if (isCurrent)
          setState({
            status: "engineError",
            error: normalizeActionError(reason),
          });
      },
    );
    return () => {
      isCurrent = false;
    };
  }, [engineStatusClient, retryGeneration]);

  function commitRepository(repository: RepositoryDescriptor) {
    setState((currentState) => ({
      status: "repositoryOpen",
      repository,
      repositoryGeneration:
        currentState.status === "replacingRepository"
          ? currentState.repositoryGeneration + 1
          : 1,
    }));
  }

  const handleRepositoryRefreshed = useCallback(
    (repository: RepositoryDescriptor) => {
      setState((currentState) => {
        if (
          currentState.status !== "repositoryOpen" &&
          currentState.status !== "replacingRepository"
        ) {
          return currentState;
        }
        return { ...currentState, repository };
      });
    },
    [],
  );

  if (state.status === "checkingEngine") {
    return (
      <AppShell>
        <section className="application-state" role="status">
          <span className="state-illustration state-illustration-loading">
            <Icon name="refresh" />
          </span>
          <p className="eyebrow">Starting ChangeLens</p>
          <h2>Connecting to the local engine</h2>
          <p>
            Preparing the secure analysis process used to inspect your
            repository.
          </p>
        </section>
      </AppShell>
    );
  }
  if (state.status === "engineError") {
    const presentation = presentActionError(state.error, repositoryErrorTitles);
    return (
      <AppShell>
        <section className="application-state application-state-error">
          <span className="state-illustration">
            <Icon name="warning" />
          </span>
          <p className="eyebrow">Connection problem</p>
          <h2>{presentation.title}</h2>
          <div className="state-error-message" role="alert">
            <ul>
              {presentation.messages.map((message, index) => (
                <li key={`${state.error.errors[index]!.code}-${index}`}>
                  {message}
                </li>
              ))}
            </ul>
          </div>
          <button
            className="primary-button"
            type="button"
            onClick={() => {
              setState({ status: "checkingEngine" });
              setRetryGeneration((generation) => generation + 1);
            }}
          >
            <Icon name="refresh" />
            Retry
          </button>
        </section>
      </AppShell>
    );
  }
  if (state.status === "selectingRepository") {
    return (
      <AppShell>
        <RepositoryPickerDialog
          dismissible={false}
          onDismiss={() => undefined}
          selectFolder={() => repositoryFolderPicker.selectFolder()}
          onOpenRepository={(path) => repositoryClient.openRepository(path)}
          onRepositoryOpened={commitRepository}
        />
      </AppShell>
    );
  }

  const replacing = state.status === "replacingRepository";
  return (
    <AppShell
      repositoryIdentity={<RepositoryIdentity repository={state.repository} />}
      onOpenAnotherRepository={() =>
        setState({
          status: "replacingRepository",
          repository: state.repository,
          repositoryGeneration: state.repositoryGeneration,
        })
      }
    >
      {replacing ? (
        <RepositoryPickerDialog
          dismissible
          onDismiss={() =>
            setState({
              status: "repositoryOpen",
              repository: state.repository,
              repositoryGeneration: state.repositoryGeneration,
            })
          }
          selectFolder={() => repositoryFolderPicker.selectFolder()}
          onOpenRepository={(path) => repositoryClient.openRepository(path)}
          onRepositoryOpened={commitRepository}
        />
      ) : null}
      {comparisonClient ? (
        <RepositoryWorkspace
          key={state.repositoryGeneration}
          repository={state.repository}
          comparisonClient={comparisonClient}
          onRepositoryRefreshed={handleRepositoryRefreshed}
        />
      ) : null}
    </AppShell>
  );
}
