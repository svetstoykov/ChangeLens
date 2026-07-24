import { useCallback, useEffect, useRef, useState } from "react";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import type { ComparisonWorkspaceState } from "../Models/ComparisonWorkspaceState";
import type { ComparisonTarget } from "../Models/ComparisonTarget";
import type { ComparisonTargetPage } from "../Models/ComparisonTargetPage";
import type { PreparedComparison } from "../Models/PreparedComparison";

interface UseComparisonControllerOptions {
  readonly repository: RepositoryDescriptor;
  readonly comparisonClient: ComparisonClient;
  readonly onRepositoryRefreshed: (repository: RepositoryDescriptor) => void;
}

interface ComparisonController {
  readonly state: ComparisonWorkspaceState;
  readonly selectTarget: (target: ComparisonTarget) => void;
  readonly setQuery: (query: string) => void;
  readonly loadMore: () => void;
  readonly refresh: () => void;
  readonly checkFreshness: () => void;
  readonly resetSearch: () => void;
}

const initialState: ComparisonWorkspaceState = {
  targets: [],
  selectedTarget: null,
  preparedComparison: null,
  freshness: "unknown",
  error: null,
  query: "",
  nextCursor: null,
  targetSetToken: null,
  unsupportedTargetCount: 0,
  isDiscovering: false,
  isPreparing: false,
};

export function useComparisonController({
  repository,
  comparisonClient,
  onRepositoryRefreshed,
}: UseComparisonControllerOptions): ComparisonController {
  const [state, setState] = useState<ComparisonWorkspaceState>(initialState);
  const stateRef = useRef(state);
  const generationRef = useRef(0);
  const queryTimerRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  const prepare = useCallback(
    async (target: ComparisonTarget, generation = generationRef.current) => {
      setState((current) => ({
        ...current,
        selectedTarget: target,
        error: null,
        isPreparing: true,
      }));
      try {
        const prepared = await comparisonClient.prepare({
          path: repository.canonicalPath,
          target: target.fullName,
        });
        if (generation !== generationRef.current) return;
        applyPreparedComparison(
          prepared,
          target,
          setState,
          onRepositoryRefreshed,
        );
      } catch (reason: unknown) {
        if (generation !== generationRef.current) return;
        const error = normalizeActionError(reason);
        setState((current) => ({
          ...current,
          error,
          freshness: current.preparedComparison ? "stale" : "unknown",
          isPreparing: false,
        }));
      }
    },
    [comparisonClient, onRepositoryRefreshed, repository.canonicalPath],
  );

  const applyPage = useCallback(
    (page: ComparisonTargetPage, append: boolean, generation: number) => {
      if (generation !== generationRef.current) return;
      const current = stateRef.current;
      const targets = append
        ? mergeTargets(current.targets, page.targets)
        : page.targets;
      const suggested =
        current.selectedTarget === null && !append
          ? page.suggestedTarget
          : null;
      setState((value) => ({
        ...value,
        targets,
        selectedTarget: value.selectedTarget ?? suggested,
        nextCursor: page.nextCursor,
        targetSetToken: page.targetSetToken,
        unsupportedTargetCount: page.unsupportedTargetCount,
        error: null,
        isDiscovering: false,
      }));
      if (suggested !== null) void prepare(suggested, generation);
    },
    [prepare],
  );

  const discover = useCallback(
    async (query: string, append = false) => {
      const generation = ++generationRef.current;
      const prior = stateRef.current;
      const after = append ? prior.nextCursor : null;
      const targetSetToken = append ? prior.targetSetToken : null;
      setState((current) => ({
        ...current,
        query,
        isDiscovering: true,
        error: null,
        ...(append
          ? {}
          : { targets: [], nextCursor: null, targetSetToken: null }),
      }));
      try {
        const page = await comparisonClient.listTargets({
          path: repository.canonicalPath,
          ...(query === "" ? {} : { query }),
          ...(after === null ? {} : { after }),
          ...(targetSetToken === null ? {} : { targetSetToken }),
        });
        applyPage(page, append, generation);
      } catch (reason: unknown) {
        if (generation !== generationRef.current) return;
        setState((current) => ({
          ...current,
          error: normalizeActionError(reason),
          isDiscovering: false,
        }));
      }
    },
    [applyPage, comparisonClient, repository.canonicalPath],
  );

  useEffect(() => {
    generationRef.current += 1;
    setState(initialState);
    void discover("");
    return () => {
      generationRef.current += 1;
      if (queryTimerRef.current !== undefined) {
        window.clearTimeout(queryTimerRef.current);
      }
    };
  }, [discover, repository.canonicalPath]);

  const selectTarget = useCallback(
    (target: ComparisonTarget) => {
      const generation = ++generationRef.current;
      void prepare(target, generation);
    },
    [prepare],
  );

  const setQuery = useCallback(
    (query: string) => {
      setState((current) => ({ ...current, query }));
      if (queryTimerRef.current !== undefined) {
        window.clearTimeout(queryTimerRef.current);
      }
      queryTimerRef.current = window.setTimeout(() => {
        void discover(query);
      }, 250);
    },
    [discover],
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
    if (current.selectedTarget === null || current.preparedComparison === null)
      return;
    const generation = ++generationRef.current;
    setState((value) => ({ ...value, freshness: "checking", error: null }));
    try {
      const freshness = await comparisonClient.checkFreshness({
        path: repository.canonicalPath,
        target: current.selectedTarget.fullName,
        freshnessToken: current.preparedComparison.freshnessToken,
      });
      if (generation !== generationRef.current) return;
      setState((value) => ({ ...value, freshness: freshness.state }));
    } catch (reason: unknown) {
      if (generation !== generationRef.current) return;
      setState((value) => ({
        ...value,
        freshness: "unknown",
        error: normalizeActionError(reason),
      }));
    }
  }, [comparisonClient, repository.canonicalPath]);

  useEffect(() => {
    if (state.preparedComparison === null || state.selectedTarget === null)
      return;
    const onFocus = () => void checkFreshness();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [checkFreshness, state.preparedComparison, state.selectedTarget]);

  const refresh = useCallback(async () => {
    const target = stateRef.current.selectedTarget;
    if (target === null) return;
    const generation = ++generationRef.current;
    setState((current) => ({ ...current, isDiscovering: true, error: null }));
    try {
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
        exactTarget = page.targets.find(
          (item) => item.fullName === target.fullName,
        );
        after = page.nextCursor ?? undefined;
        targetSetToken = page.targetSetToken;
      } while (exactTarget === undefined && after !== undefined);
      if (generation !== generationRef.current) return;
      if (exactTarget === undefined) {
        setState((current) => ({
          ...current,
          selectedTarget: null,
          freshness: current.preparedComparison ? "stale" : "unknown",
          isDiscovering: false,
        }));
        return;
      }
      setState((current) => ({ ...current, isDiscovering: false }));
      await prepare(exactTarget, generation);
    } catch (reason: unknown) {
      if (generation !== generationRef.current) return;
      setState((current) => ({
        ...current,
        isDiscovering: false,
        freshness: current.preparedComparison ? "stale" : "unknown",
        error: normalizeActionError(reason),
      }));
    }
  }, [comparisonClient, prepare, repository.canonicalPath]);

  const resetSearch = useCallback(() => void discover(""), [discover]);

  return {
    state,
    selectTarget,
    setQuery,
    loadMore,
    refresh: () => void refresh(),
    checkFreshness: () => void checkFreshness(),
    resetSearch,
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

function applyPreparedComparison(
  prepared: PreparedComparison,
  target: ComparisonTarget,
  setState: React.Dispatch<React.SetStateAction<ComparisonWorkspaceState>>,
  onRepositoryRefreshed: (repository: RepositoryDescriptor) => void,
): void {
  onRepositoryRefreshed(prepared.repository);
  setState((current) => ({
    ...current,
    selectedTarget: target,
    preparedComparison: prepared,
    freshness: "current",
    error: null,
    isPreparing: false,
  }));
}
