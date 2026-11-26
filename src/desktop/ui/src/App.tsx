import { useCallback, useEffect, useRef, useState } from "react";
import { ActionError } from "./Actions/Models/ActionError";
import { normalizeActionError } from "./Actions/Services/normalizeActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
import { AnalysisProgressView } from "./Analysis/Components/AnalysisProgressView";
import { useAnalysisRun } from "./Analysis/Hooks/useAnalysisRun";
import type { AnalysisClient } from "./Analysis/Interfaces/AnalysisClient";
import type { AnalysisRunSummary } from "./Analysis/Models/AnalysisRunSummary";
import type { AnalysisStartOutcome } from "./Analysis/Models/AnalysisStartOutcome";
import type { AnalysisStartRequest } from "./Analysis/Models/AnalysisStartRequest";
import { AppShell } from "./AppShell";
import { RepositoryWorkspace } from "./Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "./Comparisons/Interfaces/ComparisonClient";
import type { EngineStatusClient } from "./EngineStatus/Interfaces/EngineStatusClient";
import { RemoveRepositoryDialog } from "./Repositories/Components/RemoveRepositoryDialog";
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
  readonly analysisClient: AnalysisClient;
  readonly comparisonClient: ComparisonClient;
}

type Workspace = OpenedRepository & { readonly generation: number };

interface RetainedAnalysisRun {
  readonly runId: string;
  readonly attached: boolean;
}

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
  | {
      readonly status: "replacingRepository";
      readonly workspace: Workspace;
      readonly activeRun: RetainedAnalysisRun | null;
    }
  | {
      readonly status: "repositoryHistory";
      readonly workspace: Workspace | null;
      readonly activeRun: RetainedAnalysisRun | null;
    }
  | {
      readonly status: "analysisRun";
      readonly workspace: Workspace;
      readonly runId: string;
      readonly attached: boolean;
    };

export function App({
  engineStatusClient,
  repositoryClient,
  repositoryHistoryClient,
  repositoryFolderPicker,
  colorThemePreferenceClient,
  analysisClient,
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
  const [pendingRemoval, setPendingRemoval] = useState<RecentRepository | null>(
    null,
  );
  const [retryGeneration, setRetryGeneration] = useState(0);
  const [explicitTheme, setExplicitTheme] = useState<ColorTheme | null>(null);
  const [colorTheme, setColorTheme] = useState<ColorTheme>(getSystemColorTheme);
  const [themeError, setThemeError] = useState<ActionError | null>(null);
  const [retainedAnalysisRuns, setRetainedAnalysisRuns] = useState<
    ReadonlyMap<string, RetainedAnalysisRun>
  >(new Map());
  const [analysisError, setAnalysisError] = useState<ActionError | null>(null);

  const displayedRun: RetainedAnalysisRun | null =
    state.status === "analysisRun"
      ? { runId: state.runId, attached: state.attached }
      : state.status === "replacingRepository" ||
          state.status === "repositoryHistory"
        ? state.activeRun
        : null;
  const displayedRunId = displayedRun?.runId ?? null;
  const displayedRunAttached = displayedRun?.attached ?? false;

  const activeDisplayedRunIdRef = useRef<string | null>(null);
  const openEpochRef = useRef(0);

  useEffect(() => {
    activeDisplayedRunIdRef.current = displayedRunId;
  }, [displayedRunId]);

  const analysisRun = useAnalysisRun(analysisClient, displayedRunId);

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
    openEpochRef.current += 1;

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
          if (restoration.state === "none") {
            setState({ status: "selectingRepository" });
            return;
          }

          const workspace: Workspace = {
            repositoryId: restoration.repositoryId,
            repository: restoration.repository,
            preferredTarget: restoration.preferredTarget,
            generation: 1,
          };
          const activeRunId = await readActiveRunId(
            analysisClient,
            restoration.repository.canonicalPath,
          );
          if (!isCurrent) return;
          if (activeRunId === null) {
            setState({ status: "repositoryOpen", workspace });
            return;
          }

          setRetainedAnalysisRuns((current) =>
            new Map(current).set(restoration.repositoryId, {
              runId: activeRunId,
              attached: true,
            }),
          );
          setState({
            status: "analysisRun",
            workspace,
            runId: activeRunId,
            attached: true,
          });
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
    analysisClient,
    colorThemePreferenceClient,
    engineStatusClient,
    repositoryHistoryClient,
    retryGeneration,
  ]);

  async function toggleColorTheme(): Promise<void> {
    const nextTheme = colorTheme === "light" ? "dark" : "light";
    try {
      await colorThemePreferenceClient.setColorTheme(nextTheme);
      setThemeError(null);
      setExplicitTheme(nextTheme);
      setColorTheme(nextTheme);
      applyColorTheme(nextTheme);
    } catch (reason: unknown) {
      setThemeError(normalizeActionError(reason));
    }
  }

  function commitRepository(opened: OpenedRepository) {
    setAnalysisError(null);
    setState((current) => ({
      status: "repositoryOpen",
      workspace: nextWorkspace(current, opened),
    }));
    void refreshHistory();
  }

  function attachToAnalysisRun(
    opened: OpenedRepository,
    run: RetainedAnalysisRun,
  ) {
    setAnalysisError(null);
    setRetainedAnalysisRuns((current) =>
      new Map(current).set(opened.repositoryId, run),
    );
    setState((current) => ({
      status: "analysisRun",
      workspace: attachmentWorkspace(current, opened),
      runId: run.runId,
      attached: run.attached,
    }));
    void refreshHistory();
  }

  async function openOrAttachToRepository(
    opened: OpenedRepository,
    openEpoch: number,
  ): Promise<void> {
    if (openEpoch !== openEpochRef.current) {
      return;
    }

    const retainedForRepository = retainedAnalysisRuns.get(opened.repositoryId);
    if (retainedForRepository !== undefined) {
      attachToAnalysisRun(opened, retainedForRepository);
      return;
    }

    const activeRunId = await readActiveRunId(
      analysisClient,
      opened.repository.canonicalPath,
    );
    if (openEpoch !== openEpochRef.current) {
      return;
    }

    if (activeRunId !== null) {
      attachToAnalysisRun(opened, { runId: activeRunId, attached: true });
      return;
    }

    commitRepository(opened);
  }

  async function openRepositoryFromPicker(
    opened: OpenedRepository,
    source: "selecting" | "replacing",
  ): Promise<void> {
    const openEpoch = ++openEpochRef.current;
    try {
      await openOrAttachToRepository(opened, openEpoch);
    } catch (reason: unknown) {
      if (openEpoch !== openEpochRef.current) {
        return;
      }
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else if (source === "selecting") {
        setState({ status: "selectingRepository", recoveryError: error });
      } else {
        commitRepository(opened);
        setAnalysisError(error);
      }
    }
  }

  const handleRepositoryRefreshed = useCallback(
    (repository: RepositoryDescriptor) => {
      setState((current) => {
        if (
          current.status !== "repositoryOpen" &&
          current.status !== "replacingRepository" &&
          current.status !== "repositoryHistory"
        ) {
          return current;
        }
        if (
          current.workspace === null ||
          current.workspace.repository.canonicalPath !==
            repository.canonicalPath
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
    if (busyRepositoryId !== null) return;
    const openEpoch = ++openEpochRef.current;
    setBusyRepositoryId(repository.repositoryId);
    setHistoryError(null);
    try {
      const opened = await repositoryClient.openRepository(
        repository.canonicalPath,
      );
      await openOrAttachToRepository(opened, openEpoch);
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

  async function confirmRemoveRecent(): Promise<void> {
    if (pendingRemoval === null || busyRepositoryId !== null) return;
    const repository = pendingRemoval;
    setBusyRepositoryId(repository.repositoryId);
    setHistoryError(null);
    try {
      await repositoryHistoryClient.removeRecentRepository(
        repository.repositoryId,
      );
      setPendingRemoval(null);
      await refreshHistory();
    } catch (reason: unknown) {
      setPendingRemoval(null);
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

  async function startAnalysis(
    workspace: Workspace,
    request: AnalysisStartRequest,
  ): Promise<AnalysisStartOutcome> {
    if (busyRepositoryId !== null) {
      setAnalysisError(
        new ActionError("operation", [
          {
            type: "Conflict",
            code: "analysis.startInProgress",
            message:
              "Another action is still running for this repository. Wait for it to finish, then start the analysis again.",
          },
        ]),
      );
      return "unavailable";
    }

    setAnalysisError(null);
    setBusyRepositoryId(workspace.repositoryId);
    try {
      const result = await analysisClient.start(
        workspace.repository.canonicalPath,
        request.target,
        request.freshnessToken,
        request.changeContext,
      );
      if (result.state === "rejectedStale") return "stale";
      if (result.state === "accepted") {
        attachToAnalysisRun(workspace, {
          runId: result.runId,
          attached: false,
        });
        return "accepted";
      }

      const existingRun = await readRunSummary(
        analysisClient,
        result.activeRunId,
      );
      if (
        existingRun === null ||
        existingRun.repository.repositoryId !== workspace.repositoryId
      ) {
        setAnalysisError(
          new ActionError("operation", [
            {
              type: "Conflict",
              code: "analysis.activeRunUnavailable",
              message:
                "An analysis run is already active for this repository, but ChangeLens could not read it. Try again in a moment.",
            },
          ]),
        );
        return "unavailable";
      }

      attachToAnalysisRun(workspace, {
        runId: existingRun.runId,
        attached: true,
      });
      return "attached";
    } catch (reason: unknown) {
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else {
        setAnalysisError(error);
      }
      return "unavailable";
    } finally {
      setBusyRepositoryId(null);
    }
  }

  function cancelAnalysisRun(runId: string): void {
    setAnalysisError(null);
    analysisClient.cancel(runId).catch((reason: unknown) => {
      if (activeDisplayedRunIdRef.current !== runId) {
        return;
      }
      const error = normalizeActionError(reason);
      if (isLocalStateError(error)) {
        setState({ status: "localStateError", error });
      } else {
        setAnalysisError(error);
      }
    });
  }

  async function returnToSetup(workspace: Workspace): Promise<void> {
    openEpochRef.current += 1;
    setAnalysisError(null);
    setRetainedAnalysisRuns((current) => {
      const next = new Map(current);
      next.delete(workspace.repositoryId);
      return next;
    });
    setBusyRepositoryId(workspace.repositoryId);
    try {
      commitRepository(
        await repositoryClient.openRepository(
          workspace.repository.canonicalPath,
        ),
      );
    } catch (reason: unknown) {
      const error = normalizeActionError(reason);
      setState(
        isLocalStateError(error)
          ? { status: "localStateError", error }
          : { status: "selectingRepository", recoveryError: error },
      );
    } finally {
      setBusyRepositoryId(null);
    }
  }

  const shellThemeProps = {
    colorTheme,
    onToggleColorTheme: () => void toggleColorTheme(),
    themeError,
    onDismissThemeError: () => setThemeError(null),
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
        currentDestination="change"
        onShowRepositories={() => {
          openEpochRef.current += 1;
          setState({
            status: "repositoryHistory",
            workspace: null,
            activeRun: null,
          });
          void refreshHistory();
        }}
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
          onRepositoryOpened={(opened) =>
            void openRepositoryFromPicker(opened, "selecting")
          }
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
        openEpochRef.current += 1;
        setState({
          status: "repositoryHistory",
          workspace,
          activeRun: displayedRun,
        });
        void refreshHistory();
      }}
      onShowCurrentChange={() => {
        openEpochRef.current += 1;
        if (!workspace) return;
        setState(
          displayedRun === null
            ? { status: "repositoryOpen", workspace }
            : {
                status: "analysisRun",
                workspace,
                runId: displayedRun.runId,
                attached: displayedRun.attached,
              },
        );
      }}
      onOpenAnotherRepository={() => {
        openEpochRef.current += 1;
        if (workspace) {
          setState({
            status: "replacingRepository",
            workspace,
            activeRun: displayedRun,
          });
        }
      }}
    >
      {replacing && workspace ? (
        <RepositoryPickerDialog
          dismissible
          onDismiss={() => {
            openEpochRef.current += 1;
            setState(
              displayedRun === null
                ? { status: "repositoryOpen", workspace }
                : {
                    status: "analysisRun",
                    workspace,
                    runId: displayedRun.runId,
                    attached: displayedRun.attached,
                  },
            );
          }}
          selectFolder={() => repositoryFolderPicker.selectFolder()}
          onOpenRepository={(path) => repositoryClient.openRepository(path)}
          onRepositoryOpened={(opened) =>
            void openRepositoryFromPicker(opened, "replacing")
          }
        />
      ) : null}
      {pendingRemoval !== null ? (
        <RemoveRepositoryDialog
          repository={pendingRemoval}
          busy={busyRepositoryId === pendingRemoval.repositoryId}
          onConfirm={() => void confirmRemoveRecent()}
          onCancel={() => {
            if (busyRepositoryId === null) {
              setPendingRemoval(null);
            }
          }}
        />
      ) : null}
      {showingHistory ? (
        <RepositoriesDestination
          repositories={history.repositories}
          currentRepositoryId={workspace?.repositoryId ?? null}
          error={historyError}
          busyRepositoryId={busyRepositoryId}
          onOpen={(repository) => void openRecent(repository)}
          onRemove={(repository) => setPendingRemoval(repository)}
          onChooseAnother={() => {
            openEpochRef.current += 1;
            if (workspace) {
              setState({
                status: "replacingRepository",
                workspace,
                activeRun: displayedRun,
              });
            } else {
              setState({ status: "selectingRepository" });
            }
          }}
        />
      ) : null}
      {workspace && displayedRunId !== null ? (
        <div hidden={showingHistory}>
          <AnalysisProgressView
            summary={analysisRun.summary}
            error={analysisRun.error}
            actionError={analysisError}
            attached={displayedRunAttached}
            onRetry={analysisRun.retry}
            onCancel={() => cancelAnalysisRun(displayedRunId)}
            onDismissActionError={() => setAnalysisError(null)}
            onReturnToSetup={() => void returnToSetup(workspace)}
          />
        </div>
      ) : null}
      {workspace && displayedRunId === null ? (
        <div hidden={showingHistory}>
          <RepositoryWorkspace
            key={workspace.generation}
            repository={workspace.repository}
            preferredTarget={workspace.preferredTarget}
            comparisonClient={comparisonClient}
            startingAnalysis={busyRepositoryId === workspace.repositoryId}
            analysisError={analysisError}
            onRepositoryRefreshed={handleRepositoryRefreshed}
            onStartAnalysis={(request) => startAnalysis(workspace, request)}
            onDismissAnalysisError={() => setAnalysisError(null)}
          />
        </div>
      ) : null}
    </AppShell>
  );
}

function isLocalStateError(error: ActionError): boolean {
  return error.errors.some((detail) => detail.code.startsWith("localState."));
}

function nextWorkspace(
  current: ApplicationState,
  opened: OpenedRepository,
): Workspace {
  const previousWorkspace = "workspace" in current ? current.workspace : null;
  return { ...opened, generation: (previousWorkspace?.generation ?? 0) + 1 };
}

function attachmentWorkspace(
  current: ApplicationState,
  opened: OpenedRepository,
): Workspace {
  const previousWorkspace = "workspace" in current ? current.workspace : null;
  return previousWorkspace !== null &&
    previousWorkspace.repositoryId === opened.repositoryId
    ? { ...previousWorkspace, ...opened }
    : nextWorkspace(current, opened);
}

async function readActiveRunId(
  analysisClient: AnalysisClient,
  canonicalPath: string,
): Promise<string | null> {
  const result = await analysisClient.getActive(canonicalPath);
  return result.state === "active" ? result.run.runId : null;
}

async function readRunSummary(
  analysisClient: AnalysisClient,
  runId: string,
): Promise<AnalysisRunSummary | null> {
  try {
    return await analysisClient.pollRun(runId);
  } catch {
    return null;
  }
}
