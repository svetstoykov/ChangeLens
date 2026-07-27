import { useCallback, useEffect, useRef, useState } from "react";
import { ActionError } from "../../Actions/Models/ActionError";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import type { ComparisonWorkspaceState } from "../Models/ComparisonWorkspaceState";
import type { ComparisonTarget } from "../Models/ComparisonTarget";

interface UseComparisonControllerOptions {
  readonly repository: RepositoryDescriptor;
  readonly comparisonClient: ComparisonClient;
  readonly preferredTarget: string | null;
  readonly onRepositoryRefreshed: (repository: RepositoryDescriptor) => void;
}

interface ComparisonController {
  readonly state: ComparisonWorkspaceState;
  readonly selectTarget: (target: ComparisonTarget) => void;
  readonly setQuery: (query: string) => void;
  readonly loadMore: () => void;
  readonly refresh: () => void;
  readonly checkFreshness: () => void;
  readonly refreshRemoteBaseline: () => void;
  readonly cancelRemoteBaselineRefresh: () => void;
  readonly resetSearch: () => void;
  readonly retryDiscovery: () => void;
  readonly retryPreparation: () => void;
}

const initialState: ComparisonWorkspaceState = {
  targets: [],
  selectedTarget: null,
  preparedComparison: null,
  freshness: "unknown",
  remoteBaseline: "idle",
  remoteBaselineRevision: null,
  error: null,
  errorSource: null,
  query: "",
  nextCursor: null,
  targetSetToken: null,
  unsupportedTargetCount: 0,
  isDiscovering: false,
  isPreparing: false,
  isRefreshing: false,
};

export function useComparisonController({
  repository,
  comparisonClient,
  preferredTarget,
  onRepositoryRefreshed,
}: UseComparisonControllerOptions): ComparisonController {
  const [state, setState] = useState<ComparisonWorkspaceState>(initialState);
  const stateRef = useRef(state);
  const discoveryEpochRef = useRef(0);
  const preparationEpochRef = useRef(0);
  const freshnessEpochRef = useRef(0);
  const refreshEpochRef = useRef(0);
  const remoteBaselineEpochRef = useRef(0);
  const queryTimerRef = useRef<number | undefined>(undefined);

  const updateState = useCallback(
    (
      updater: (current: ComparisonWorkspaceState) => ComparisonWorkspaceState,
    ) => {
      setState((current) => {
        const next = updater(current);
        stateRef.current = next;
        return next;
      });
    },
    [],
  );

  const prepare = useCallback(
    async (target: ComparisonTarget, refreshEpoch?: number) => {
      const preparationEpoch = ++preparationEpochRef.current;
      freshnessEpochRef.current += 1;
      updateState((current) => ({
        ...current,
        selectedTarget: target,
        freshness: current.preparedComparison ? "stale" : "unknown",
        error: null,
        errorSource: null,
        isPreparing: true,
      }));

      try {
        const prepared = await comparisonClient.prepare({
          path: repository.canonicalPath,
          target: target.fullName,
        });
        if (
          preparationEpoch !== preparationEpochRef.current ||
          (refreshEpoch !== undefined &&
            refreshEpoch !== refreshEpochRef.current)
        ) {
          return;
        }

        freshnessEpochRef.current += 1;
        onRepositoryRefreshed(prepared.repository);
        updateState((current) => ({
          ...current,
          selectedTarget: prepared.target,
          preparedComparison: prepared,
          freshness: "current",
          error: null,
          errorSource: null,
          isPreparing: false,
          isRefreshing: false,
        }));
      } catch (reason: unknown) {
        if (
          preparationEpoch !== preparationEpochRef.current ||
          (refreshEpoch !== undefined &&
            refreshEpoch !== refreshEpochRef.current)
        ) {
          return;
        }

        updateState((current) => ({
          ...current,
          error: normalizeActionError(reason),
          errorSource: "preparation",
          freshness: current.preparedComparison ? "stale" : "unknown",
          isPreparing: false,
          isRefreshing: false,
        }));
      }
    },
    [
      comparisonClient,
      onRepositoryRefreshed,
      repository.canonicalPath,
      updateState,
    ],
  );

  const checkRemoteBaseline = useCallback(
    async (target: ComparisonTarget) => {
      if (target.kind !== "remoteTracking") {
        remoteBaselineEpochRef.current += 1;
        updateState((current) => ({
          ...current,
          remoteBaseline: "idle",
          remoteBaselineRevision: null,
        }));
        return;
      }

      const remoteBaselineEpoch = ++remoteBaselineEpochRef.current;
      updateState((current) => ({ ...current, remoteBaseline: "checking" }));

      try {
        const result = await comparisonClient.checkRemoteBaseline({
          path: repository.canonicalPath,
          target: target.fullName,
        });
        if (remoteBaselineEpoch !== remoteBaselineEpochRef.current) return;
        updateState((current) => ({
          ...current,
          remoteBaseline: result.state === "noRemote" ? "idle" : result.state,
          remoteBaselineRevision:
            result.state === "noRemote" ? null : result.remoteRevision,
        }));
      } catch {
        if (remoteBaselineEpoch !== remoteBaselineEpochRef.current) return;
        updateState((current) => ({
          ...current,
          remoteBaseline: "unreachable",
        }));
      }
    },
    [comparisonClient, repository.canonicalPath, updateState],
  );

  const discover = useCallback(
    async (query: string, append = false) => {
      async function requestPage(
        appendPage: boolean,
        allowContinuationRecovery: boolean,
      ): Promise<void> {
        const discoveryEpoch = ++discoveryEpochRef.current;
        const prior = stateRef.current;
        const after = appendPage ? prior.nextCursor : null;
        const targetSetToken = appendPage ? prior.targetSetToken : null;
        updateState((current) => ({
          ...current,
          query,
          isDiscovering: true,
          error: null,
          errorSource: null,
          ...(appendPage
            ? {}
            : {
                targets: [],
                nextCursor: null,
                targetSetToken: null,
                unsupportedTargetCount: 0,
              }),
        }));

        try {
          const page = await comparisonClient.listTargets({
            path: repository.canonicalPath,
            ...(query === "" ? {} : { query }),
            ...(after === null ? {} : { after }),
            ...(targetSetToken === null ? {} : { targetSetToken }),
          });
          if (discoveryEpoch !== discoveryEpochRef.current) return;

          const current = stateRef.current;
          const targets = appendPage
            ? mergeTargets(current.targets, page.targets)
            : page.targets;
          const suggested =
            preferredTarget === null &&
            current.selectedTarget === null &&
            !appendPage
              ? page.suggestedTarget
              : null;
          updateState((value) => ({
            ...value,
            targets,
            selectedTarget: value.selectedTarget ?? suggested,
            nextCursor: page.nextCursor,
            targetSetToken: page.targetSetToken,
            unsupportedTargetCount: page.unsupportedTargetCount,
            error: null,
            errorSource: null,
            isDiscovering: false,
          }));
          if (suggested !== null) {
            refreshEpochRef.current += 1;
            void prepare(suggested);
            void checkRemoteBaseline(suggested);
          }
        } catch (reason: unknown) {
          if (discoveryEpoch !== discoveryEpochRef.current) return;
          const error = normalizeActionError(reason);
          const code = error.errors[0].code;
          if (
            appendPage &&
            allowContinuationRecovery &&
            (code === "comparison.invalidTargetPage" ||
              code === "comparison.targetsChanged")
          ) {
            updateState((current) => ({
              ...current,
              targets: [],
              nextCursor: null,
              targetSetToken: null,
              unsupportedTargetCount: 0,
              error: null,
              errorSource: null,
              isDiscovering: false,
            }));
            await requestPage(false, false);
            return;
          }

          updateState((current) => ({
            ...current,
            error,
            errorSource: "discovery",
            isDiscovering: false,
          }));
        }
      }

      await requestPage(append, append);
    },
    [
      checkRemoteBaseline,
      comparisonClient,
      preferredTarget,
      prepare,
      repository.canonicalPath,
      updateState,
    ],
  );

  useEffect(() => {
    discoveryEpochRef.current += 1;
    preparationEpochRef.current += 1;
    freshnessEpochRef.current += 1;
    refreshEpochRef.current += 1;
    remoteBaselineEpochRef.current += 1;
    stateRef.current = initialState;
    setState(initialState);
    if (preferredTarget === null) {
      void discover("");
    } else {
      void (async () => {
        try {
          let after: string | undefined;
          let targetSetToken: string | undefined;
          let exactTarget: ComparisonTarget | undefined;
          const query = preferredTarget.split("/").at(-1) ?? preferredTarget;
          do {
            const page = await comparisonClient.listTargets({
              path: repository.canonicalPath,
              query,
              ...(after === undefined ? {} : { after }),
              ...(targetSetToken === undefined ? {} : { targetSetToken }),
            });
            exactTarget = page.targets.find(
              (target) => target.fullName === preferredTarget,
            );
            after = page.nextCursor ?? undefined;
            targetSetToken = page.targetSetToken;
          } while (exactTarget === undefined && after !== undefined);

          if (exactTarget) {
            updateState((current) => ({
              ...current,
              targets: [exactTarget],
              selectedTarget: exactTarget,
              isDiscovering: false,
            }));
            void checkRemoteBaseline(exactTarget);
            await prepare(exactTarget);
          } else {
            await discover("");
            updateState((current) => ({
              ...current,
              error: new ActionError("operation", [
                {
                  type: "NotFound",
                  code: "comparison.targetUnavailable",
                  message:
                    "The saved comparison target is unavailable. Choose another target.",
                },
              ]),
              errorSource: "preparation",
              isDiscovering: false,
            }));
          }
        } catch (reason: unknown) {
          updateState((current) => ({
            ...current,
            error: normalizeActionError(reason),
            errorSource: "preparation",
            isDiscovering: false,
          }));
        }
      })();
    }

    return () => {
      discoveryEpochRef.current += 1;
      preparationEpochRef.current += 1;
      freshnessEpochRef.current += 1;
      refreshEpochRef.current += 1;
      remoteBaselineEpochRef.current += 1;
      if (queryTimerRef.current !== undefined) {
        window.clearTimeout(queryTimerRef.current);
      }
    };
  }, [
    checkRemoteBaseline,
    comparisonClient,
    discover,
    preferredTarget,
    prepare,
    repository.canonicalPath,
    updateState,
  ]);

  const selectTarget = useCallback(
    (target: ComparisonTarget) => {
      refreshEpochRef.current += 1;
      void prepare(target);
      void checkRemoteBaseline(target);
    },
    [checkRemoteBaseline, prepare],
  );

  const setQuery = useCallback(
    (query: string) => {
      discoveryEpochRef.current += 1;
      updateState((current) => ({
        ...current,
        query,
        targets: [],
        nextCursor: null,
        targetSetToken: null,
        unsupportedTargetCount: 0,
        error: null,
        errorSource: null,
        isDiscovering: true,
      }));
      if (queryTimerRef.current !== undefined) {
        window.clearTimeout(queryTimerRef.current);
      }
      queryTimerRef.current = window.setTimeout(() => {
        queryTimerRef.current = undefined;
        void discover(query);
      }, 250);
    },
    [discover, updateState],
  );

  const loadMore = useCallback(() => {
    if (
      stateRef.current.nextCursor === null ||
      stateRef.current.targetSetToken === null ||
      stateRef.current.isDiscovering
    ) {
      return;
    }
    void discover(stateRef.current.query, true);
  }, [discover]);

  const checkFreshness = useCallback(async () => {
    const current = stateRef.current;
    if (
      current.selectedTarget === null ||
      current.preparedComparison === null ||
      current.isPreparing ||
      current.isRefreshing ||
      current.selectedTarget.fullName !==
        current.preparedComparison.target.fullName
    ) {
      return;
    }

    const target = current.selectedTarget.fullName;
    const freshnessToken = current.preparedComparison.freshnessToken;
    const freshnessEpoch = ++freshnessEpochRef.current;
    updateState((value) => ({
      ...value,
      freshness: "checking",
      error: null,
      errorSource: null,
    }));

    try {
      const freshness = await comparisonClient.checkFreshness({
        path: repository.canonicalPath,
        target,
        freshnessToken,
      });
      if (
        freshnessEpoch !== freshnessEpochRef.current ||
        !matchesPreparedPair(stateRef.current, target, freshnessToken)
      ) {
        return;
      }
      updateState((value) => ({ ...value, freshness: freshness.state }));
    } catch (reason: unknown) {
      if (
        freshnessEpoch !== freshnessEpochRef.current ||
        !matchesPreparedPair(stateRef.current, target, freshnessToken)
      ) {
        return;
      }
      updateState((value) => ({
        ...value,
        freshness: "unknown",
        error: normalizeActionError(reason),
        errorSource: "freshness",
      }));
    }
  }, [comparisonClient, repository.canonicalPath, updateState]);

  useEffect(() => {
    if (
      state.preparedComparison === null ||
      state.selectedTarget === null ||
      state.isPreparing ||
      state.isRefreshing ||
      state.preparedComparison.target.fullName !== state.selectedTarget.fullName
    ) {
      return;
    }

    const onFocus = () => void checkFreshness();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [
    checkFreshness,
    state.isPreparing,
    state.isRefreshing,
    state.preparedComparison,
    state.selectedTarget,
  ]);

  const findExactTargetAndPrepare = useCallback(
    async (
      target: ComparisonTarget,
      refreshEpoch: number,
    ): Promise<boolean> => {
      let after: string | undefined;
      let targetSetToken: string | undefined;
      let exactTarget: ComparisonTarget | undefined;
      do {
        const page = await comparisonClient.listTargets({
          path: repository.canonicalPath,
          query: target.name,
          ...(after === undefined ? {} : { after }),
          ...(targetSetToken === undefined ? {} : { targetSetToken }),
        });
        if (refreshEpoch !== refreshEpochRef.current) return true;
        exactTarget = page.targets.find(
          (item) => item.fullName === target.fullName,
        );
        after = page.nextCursor ?? undefined;
        targetSetToken = page.targetSetToken;
      } while (exactTarget === undefined && after !== undefined);

      if (exactTarget === undefined) {
        updateState((current) => ({
          ...current,
          selectedTarget: null,
          freshness: current.preparedComparison ? "stale" : "unknown",
          isRefreshing: false,
        }));
        return false;
      }

      await prepare(exactTarget, refreshEpoch);
      return true;
    },
    [comparisonClient, prepare, repository.canonicalPath, updateState],
  );

  const refresh = useCallback(async () => {
    const target = stateRef.current.selectedTarget;
    if (target === null) return;

    const refreshEpoch = ++refreshEpochRef.current;
    preparationEpochRef.current += 1;
    freshnessEpochRef.current += 1;
    updateState((current) => ({
      ...current,
      freshness: current.preparedComparison ? "stale" : "unknown",
      error: null,
      errorSource: null,
      isPreparing: false,
      isRefreshing: true,
    }));

    try {
      await findExactTargetAndPrepare(target, refreshEpoch);
    } catch (reason: unknown) {
      if (refreshEpoch !== refreshEpochRef.current) return;
      updateState((current) => ({
        ...current,
        freshness: current.preparedComparison ? "stale" : "unknown",
        error: normalizeActionError(reason),
        errorSource: "refresh",
        isRefreshing: false,
      }));
    }
  }, [findExactTargetAndPrepare, updateState]);

  const refreshRemoteBaseline = useCallback(async () => {
    const target = stateRef.current.selectedTarget;
    if (target === null || target.kind !== "remoteTracking") return;

    const refreshEpoch = ++refreshEpochRef.current;
    preparationEpochRef.current += 1;
    freshnessEpochRef.current += 1;
    updateState((current) => ({
      ...current,
      freshness: current.preparedComparison ? "stale" : "unknown",
      remoteBaseline: "refreshing",
      error: null,
      errorSource: null,
      isPreparing: false,
      isRefreshing: true,
    }));

    try {
      await comparisonClient.refreshRemoteBaseline({
        path: repository.canonicalPath,
        target: target.fullName,
      });
      if (refreshEpoch !== refreshEpochRef.current) return;

      const remoteBaselineEpoch = ++remoteBaselineEpochRef.current;
      await findExactTargetAndPrepare(target, refreshEpoch);
      if (
        refreshEpoch === refreshEpochRef.current &&
        remoteBaselineEpoch === remoteBaselineEpochRef.current
      ) {
        updateState((current) => ({ ...current, remoteBaseline: "current" }));
      }
    } catch (reason: unknown) {
      if (refreshEpoch !== refreshEpochRef.current) return;
      updateState((current) => ({
        ...current,
        freshness: current.preparedComparison ? "stale" : "unknown",
        remoteBaseline: "moved",
        error: normalizeActionError(reason),
        errorSource: "remoteBaseline",
        isRefreshing: false,
      }));
    }
  }, [
    comparisonClient,
    findExactTargetAndPrepare,
    repository.canonicalPath,
    updateState,
  ]);

  const cancelRemoteBaselineRefresh = useCallback(() => {
    refreshEpochRef.current += 1;
    updateState((current) => ({
      ...current,
      remoteBaseline: "moved",
      isRefreshing: false,
    }));
  }, [updateState]);

  const resetSearch = useCallback(() => {
    if (queryTimerRef.current !== undefined) {
      window.clearTimeout(queryTimerRef.current);
      queryTimerRef.current = undefined;
    }
    discoveryEpochRef.current += 1;
    void discover("");
  }, [discover]);

  const retryDiscovery = useCallback(() => {
    if (queryTimerRef.current !== undefined) {
      window.clearTimeout(queryTimerRef.current);
      queryTimerRef.current = undefined;
    }
    discoveryEpochRef.current += 1;
    void discover(stateRef.current.query);
  }, [discover]);

  const retryPreparation = useCallback(() => {
    const target = stateRef.current.selectedTarget;
    if (target === null) return;
    refreshEpochRef.current += 1;
    void prepare(target);
  }, [prepare]);

  return {
    state,
    selectTarget,
    setQuery,
    loadMore,
    refresh: () => void refresh(),
    checkFreshness: () => void checkFreshness(),
    refreshRemoteBaseline: () => void refreshRemoteBaseline(),
    cancelRemoteBaselineRefresh,
    resetSearch,
    retryDiscovery,
    retryPreparation,
  };
}

function mergeTargets(
  existing: readonly ComparisonTarget[],
  incoming: readonly ComparisonTarget[],
): readonly ComparisonTarget[] {
  const targets = new Map(existing.map((target) => [target.fullName, target]));
  for (const target of incoming) targets.set(target.fullName, target);
  return [...targets.values()];
}

function matchesPreparedPair(
  state: ComparisonWorkspaceState,
  target: string,
  freshnessToken: string,
): boolean {
  return (
    state.selectedTarget?.fullName === target &&
    state.preparedComparison?.target.fullName === target &&
    state.preparedComparison.freshnessToken === freshnessToken
  );
}
