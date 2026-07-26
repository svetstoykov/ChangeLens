import { useCallback, useEffect, useState } from "react";
import type { ActionError } from "./Actions/Models/ActionError";
import { normalizeActionError } from "./Actions/Services/normalizeActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
import { AppShell } from "./AppShell";
import { RepositoryWorkspace } from "./Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "./Comparisons/Interfaces/ComparisonClient";
import type { EngineStatusClient } from "./EngineStatus/Interfaces/EngineStatusClient";
import { RepositoriesDestination } from "./Repositories/Components/RepositoriesDestination";
import { RepositoryPickerDialog } from "./Repositories/Components/RepositoryPickerDialog";
import { repositoryErrorTitles } from "./Repositories/Constants/repositoryErrorTitles";
import type { RepositoryClient } from "./Repositories/Interfaces/RepositoryClient";
import type { RepositoryFolderPicker } from "./Repositories/Interfaces/RepositoryFolderPicker";
import type { RepositoryHistoryClient } from "./Repositories/Interfaces/RepositoryHistoryClient";
import type { OpenedRepository } from "./Repositories/Models/OpenedRepository";
import type { RecentRepository } from "./Repositories/Models/RecentRepository";
import type { RepositoryDescriptor } from "./Repositories/Models/RepositoryDescriptor";
import type { RepositoryHistory } from "./Repositories/Models/RepositoryHistory";
import { Icon } from "./Visuals/Components/Icon";
import { darkColorSchemeMediaQuery } from "./Visuals/Constants/colorThemeConstants";
import type { ColorThemePreferenceClient } from "./Visuals/Interfaces/ColorThemePreferenceClient";
import type { ColorTheme } from "./Visuals/Models/ColorTheme";
import {
  applyColorTheme,
  getSystemColorTheme,
} from "./Visuals/Services/colorThemePreference";
import "./styles.css";

interface AppProps {
  readonly engineStatusClient: EngineStatusClient;
  readonly repositoryClient: RepositoryClient;
  readonly repositoryHistoryClient: RepositoryHistoryClient;
  readonly repositoryFolderPicker: RepositoryFolderPicker;
  readonly colorThemePreferenceClient: ColorThemePreferenceClient;
  readonly comparisonClient?: ComparisonClient;
}

type Workspace = OpenedRepository & { readonly generation: number };

export type ApplicationState =
  | { readonly status: "checkingEngine" }
  | { readonly status: "loadingLocalState" }
  | { readonly status: "restoringRepository" }
  | { readonly status: "localStateError"; readonly error: ActionError }
  | { readonly status: "engineError"; readonly error: ActionError }
  | {
      readonly status: "selectingRepository";
      readonly recoveryError?: ActionError;
    }
  | { readonly status: "repositoryOpen"; readonly workspace: Workspace }
  | { readonly status: "replacingRepository"; readonly workspace: Workspace }
  | {
      readonly status: "repositoryHistory";
      readonly workspace: Workspace | null;
    };

export function App({
  engineStatusClient,
  repositoryClient,
  repositoryHistoryClient,
  repositoryFolderPicker,
  colorThemePreferenceClient,
  comparisonClient,
}: AppProps) {
  const [state, setState] = useState<ApplicationState>({
    status: "checkingEngine",
  });
  const [history, setHistory] = useState<RepositoryHistory>({
    lastRepositoryId: null,
    repositories: [],
  });
  const [historyError, setHistoryError] = useState<ActionError | null>(null);
  const [busyRepositoryId, setBusyRepositoryId] = useState<string | null>(null);
  const [retryGeneration, setRetryGeneration] = useState(0);
  const [explicitTheme, setExplicitTheme] = useState<ColorTheme | null>(null);
  const [colorTheme, setColorTheme] = useState<ColorTheme>(getSystemColorTheme);

  useEffect(() => {
    if (explicitTheme !== null) return;
    const media = window.matchMedia(darkColorSchemeMediaQuery);
    const handleChange = (event: MediaQueryListEvent) => {
      const next = event.matches ? "dark" : "light";
      setColorTheme(next);
      applyColorTheme(next);
    };
    media.addEventListener("change", handleChange);
    return () => media.removeEventListener("change", handleChange);
  }, [explicitTheme]);

  useEffect(() => {
    let isCurrent = true;

    async function start(): Promise<void> {
      try {
        await engineStatusClient.checkStatus();
        if (!isCurrent) return;
        setState({ status: "loadingLocalState" });

        const [storedTheme, recent] = await Promise.all([
          colorThemePreferenceClient.getColorTheme(),
          repositoryHistoryClient.listRecentRepositories(),
        ]);
        if (!isCurrent) return;
        setExplicitTheme(storedTheme);
        const appliedTheme = storedTheme ?? getSystemColorTheme();
        setColorTheme(appliedTheme);
        applyColorTheme(appliedTheme);
        setHistory(recent);
        setState({ status: "restoringRepository" });

        try {
          const restoration =
            await repositoryHistoryClient.restoreLastRepository();
          if (!isCurrent) return;
          setState(
            restoration.state === "none"
              ? { status: "selectingRepository" }
              : {
                  status: "repositoryOpen",
                  workspace: {
                    repositoryId: restoration.repositoryId,
                    repository: restoration.repository,
                    preferredTarget: restoration.preferredTarget,
                    generation: 1,
                  },
                },
          );
        } catch (reason: unknown) {
          if (!isCurrent) return;
          const error = normalizeActionError(reason);
          setState(
            isLocalStateError(error)
              ? { status: "localStateError", error }
              : { status: "selectingRepository", recoveryError: error },
          );
        }
      } catch (reason: unknown) {
        if (!isCurrent) return;
        const error = normalizeActionError(reason);
        setState(
          isLocalStateError(error)
            ? { status: "localStateError", error }
            : { status: "engineError", error },
        );
      }
    }

    void start();
    return () => {
      isCurrent = false;
    };
  }, [
    colorThemePreferenceClient,
    engineStatusClient,
    repositoryHistoryClient,
    retryGeneration,
  ]);

  async function toggleColorTheme(): Promise<void> {
    const nextTheme = colorTheme === "light" ? "dark" : "light";
    try {
      await colorThemePreferenceClient.setColorTheme(nextTheme);
      setExplicitTheme(nextTheme);
      setColorTheme(nextTheme);
      applyColorTheme(nextTheme);
    } catch (reason: unknown) {
      setState({
        status: "localStateError",
        error: normalizeActionError(reason),
      });
    }
  }

  function commitRepository(opened: OpenedRepository) {
    setState((current) => {
      const previousWorkspace =
        current.status === "replacingRepository" ? current.workspace : null;
      return {
        status: "repositoryOpen",
        workspace: {
          ...opened,
          generation: (previousWorkspace?.generation ?? 0) + 1,
        },
      };
    });
    void refreshHistory();
  }

  const handleRepositoryRefreshed = useCallback(
    (repository: RepositoryDescriptor) => {
      setState((current) => {
        if (
          current.status !== "repositoryOpen" &&
          current.status !== "replacingRepository"
        ) {
          return current;
        }
        return { ...current, workspace: { ...current.workspace, repository } };
      });
    },
    [],
  );

  async function refreshHistory(): Promise<void> {
    try {
      setHistory(await repositoryHistoryClient.listRecentRepositories());
      setHistoryError(null);
    } catch (reason: unknown) {
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else {
        setHistoryError(error);
      }
    }
  }

  async function openRecent(repository: RecentRepository): Promise<void> {
    setBusyRepositoryId(repository.repositoryId);
    setHistoryError(null);
    try {
      commitRepository(
        await repositoryClient.openRepository(repository.canonicalPath),
      );
    } catch (reason: unknown) {
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else {
        setHistoryError(error);
      }
    } finally {
      setBusyRepositoryId(null);
    }
  }

  async function removeRecent(repository: RecentRepository): Promise<void> {
    const confirmed = window.confirm(
      `Remove ${repository.name} from ChangeLens history? Repository files will not be changed.`,
    );
    if (!confirmed) return;
    setBusyRepositoryId(repository.repositoryId);
    try {
      await repositoryHistoryClient.removeRecentRepository(
        repository.repositoryId,
      );
      await refreshHistory();
    } catch (reason: unknown) {
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else {
        setHistoryError(error);
      }
    } finally {
      setBusyRepositoryId(null);
    }
  }

  const shellThemeProps = {
    colorTheme,
    onToggleColorTheme: () => void toggleColorTheme(),
  };

  if (
    state.status === "checkingEngine" ||
    state.status === "loadingLocalState" ||
    state.status === "restoringRepository"
  ) {
    const heading = {
      checkingEngine: "Connecting to the local engine",
      loadingLocalState: "Loading local state",
      restoringRepository: "Restoring your repository",
    }[state.status];
    return (
      <AppShell {...shellThemeProps}>
        <section className="application-state" role="status">
          <span className="state-illustration state-illustration-loading">
            <Icon name="refresh" />
          </span>
          <p className="eyebrow">Starting ChangeLens</p>
          <h2>{heading}</h2>
          <p>
            Preparing durable local workspace metadata and repository facts.
          </p>
        </section>
      </AppShell>
    );
  }

  if (state.status === "engineError" || state.status === "localStateError") {
    const presentation = presentActionError(state.error, repositoryErrorTitles);
    return (
      <AppShell {...shellThemeProps}>
        <section className="application-state application-state-error">
          <span className="state-illustration">
            <Icon name="warning" />
          </span>
          <p className="eyebrow">
            {state.status === "localStateError"
              ? "Local state unavailable"
              : "Connection problem"}
          </p>
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
    const recovery = state.recoveryError
      ? presentActionError(state.recoveryError, repositoryErrorTitles)
      : null;
    return (
      <AppShell
        {...shellThemeProps}
        showRepositoryNavigation
        onShowRepositories={() =>
          setState({ status: "repositoryHistory", workspace: null })
        }
      >
        {recovery ? (
          <section className="startup-recovery" role="alert">
            <strong>{recovery.title}</strong>
            <p>{recovery.messages.join(" ")}</p>
          </section>
        ) : null}
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

  const workspace = state.workspace;
  const replacing = state.status === "replacingRepository";
  const showingHistory = state.status === "repositoryHistory";
  return (
    <AppShell
      {...shellThemeProps}
      hasRepository={workspace !== null}
      showRepositoryNavigation
      currentDestination={showingHistory ? "repositories" : "change"}
      onShowRepositories={() => {
        setState({ status: "repositoryHistory", workspace });
        void refreshHistory();
      }}
      onShowCurrentChange={() => {
        if (workspace) setState({ status: "repositoryOpen", workspace });
      }}
      onOpenAnotherRepository={() => {
        if (workspace) setState({ status: "replacingRepository", workspace });
      }}
    >
      {replacing && workspace ? (
        <RepositoryPickerDialog
          dismissible
          onDismiss={() => setState({ status: "repositoryOpen", workspace })}
          selectFolder={() => repositoryFolderPicker.selectFolder()}
          onOpenRepository={(path) => repositoryClient.openRepository(path)}
          onRepositoryOpened={commitRepository}
        />
      ) : null}
      {showingHistory ? (
        <RepositoriesDestination
          repositories={history.repositories}
          currentRepositoryId={workspace?.repositoryId ?? null}
          error={historyError}
          busyRepositoryId={busyRepositoryId}
          onOpen={(repository) => void openRecent(repository)}
          onRemove={(repository) => void removeRecent(repository)}
          onChooseAnother={() => {
            if (workspace)
              setState({ status: "replacingRepository", workspace });
            else setState({ status: "selectingRepository" });
          }}
        />
      ) : workspace && comparisonClient ? (
        <RepositoryWorkspace
          key={workspace.generation}
          repository={workspace.repository}
          preferredTarget={workspace.preferredTarget}
          comparisonClient={comparisonClient}
          onRepositoryRefreshed={handleRepositoryRefreshed}
        />
      ) : null}
    </AppShell>
  );
}

function isLocalStateError(error: ActionError): boolean {
  return error.errors.some((detail) => detail.code.startsWith("localState."));
}
