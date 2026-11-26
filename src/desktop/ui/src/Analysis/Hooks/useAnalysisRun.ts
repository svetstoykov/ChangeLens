import { useEffect, useRef, useState } from "react";
import type { ActionError } from "../../Actions/Models/ActionError";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { AnalysisClient } from "../Interfaces/AnalysisClient";
import type { AnalysisRunState } from "../Models/AnalysisRunState";
import type { AnalysisRunSummary } from "../Models/AnalysisRunSummary";

const VISIBLE_POLL_DELAY_MS = 250;
const HIDDEN_POLL_INTERVAL_MS = 2_000;

const TERMINAL_OR_INTERRUPTED_STATES: ReadonlySet<AnalysisRunState> = new Set([
  "completed",
  "completedWithLimitations",
  "cancelled",
  "failed",
  "interrupted",
]);

interface PollState {
  readonly runId: string;
  readonly summary: AnalysisRunSummary | null;
  readonly error: ActionError | null;
}

export interface UseAnalysisRunResult {
  readonly summary: AnalysisRunSummary | null;
  readonly error: ActionError | null;
  readonly retry: () => void;
}

export function useAnalysisRun(
  client: AnalysisClient,
  runId: string | null,
): UseAnalysisRunResult {
  const [pollState, setPollState] = useState<PollState | null>(null);
  const pollNowRef = useRef<(() => void) | null>(null);

  const currentPollState =
    runId !== null && pollState?.runId === runId ? pollState : null;
  const summary = currentPollState?.summary ?? null;
  const error = currentPollState?.error ?? null;

  useEffect(() => {
    if (runId === null) {
      return;
    }

    let isActive = true;
    let isTerminal = false;
    let isPolling = false;
    let pollSequence = 0;
    let timerId: number | null = null;

    const clearScheduledPoll = () => {
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    const runPoll = () => {
      if (!isActive || isTerminal || isPolling) {
        return;
      }

      isPolling = true;
      const sequence = ++pollSequence;
      client
        .pollRun(runId)
        .then((result) => {
          if (!isActive || sequence !== pollSequence) {
            return;
          }

          isPolling = false;
          setPollState({ runId, summary: result, error: null });

          if (TERMINAL_OR_INTERRUPTED_STATES.has(result.state)) {
            isTerminal = true;
            clearScheduledPoll();
            document.removeEventListener(
              "visibilitychange",
              handleVisibilityChange,
            );
            return;
          }

          clearScheduledPoll();
          timerId = window.setTimeout(
            runPoll,
            document.hidden ? HIDDEN_POLL_INTERVAL_MS : VISIBLE_POLL_DELAY_MS,
          );
        })
        .catch((pollError: unknown) => {
          if (!isActive || sequence !== pollSequence) {
            return;
          }

          isPolling = false;
          const normalized = normalizeActionError(pollError);
          setPollState((current) => ({
            runId,
            summary: current?.runId === runId ? current.summary : null,
            error: normalized,
          }));
        });
    };

    const handleVisibilityChange = () => {
      if (!document.hidden && !isTerminal) {
        clearScheduledPoll();
        runPoll();
      }
    };

    pollNowRef.current = runPoll;
    document.addEventListener("visibilitychange", handleVisibilityChange);
    runPoll();

    return () => {
      isActive = false;
      pollSequence += 1;
      pollNowRef.current = null;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      clearScheduledPoll();
    };
  }, [client, runId]);

  const retry = (): void => {
    if (runId === null) {
      return;
    }
    setPollState((current) =>
      current !== null && current.runId === runId
        ? { ...current, error: null }
        : null,
    );
    pollNowRef.current?.();
  };

  return { summary, error, retry };
}
