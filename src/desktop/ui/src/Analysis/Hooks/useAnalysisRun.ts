import { useEffect, useRef, useState } from "react";
import type { ActionError } from "../../Actions/Models/ActionError";
import type { AnalysisClient } from "../Interfaces/AnalysisClient";
import type { AnalysisRunProjection } from "../Models/AnalysisRunProjection";

const VISIBLE_POLL_DELAY_MS = 250;
const HIDDEN_POLL_INTERVAL_MS = 2_000;

const TERMINAL_OR_INTERRUPTED_STATES: ReadonlySet<string> = new Set([
  "completed",
  "completedWithLimitations",
  "cancelled",
  "failed",
  "interrupted",
]);

export interface UseAnalysisRunResult {
  readonly projection: AnalysisRunProjection | null;
  readonly error: ActionError | null;
  readonly retry: () => void;
}

export function useAnalysisRun(
  client: AnalysisClient,
  runId: string | null,
): UseAnalysisRunResult {
  const [projection, setProjection] = useState<AnalysisRunProjection | null>(
    null,
  );
  const [error, setError] = useState<ActionError | null>(null);
  const pollNowRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setProjection(null);
    setError(null);
    pollNowRef.current = null;

    if (runId === null) {
      return;
    }

    let isActive = true;
    let timerId: number | null = null;

    const clearScheduledPoll = () => {
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    const runPoll = () => {
      client
        .pollRun(runId)
        .then((result) => {
          if (!isActive) {
            return;
          }

          setProjection(result);
          setError(null);

          if (!TERMINAL_OR_INTERRUPTED_STATES.has(result.state)) {
            clearScheduledPoll();
            timerId = window.setTimeout(
              runPoll,
              document.hidden ? HIDDEN_POLL_INTERVAL_MS : VISIBLE_POLL_DELAY_MS,
            );
          }
        })
        .catch((pollError: unknown) => {
          if (!isActive) {
            return;
          }

          setError(pollError as ActionError);
        });
    };

    const handleVisibilityChange = () => {
      if (!document.hidden) {
        clearScheduledPoll();
        runPoll();
      }
    };

    pollNowRef.current = runPoll;
    document.addEventListener("visibilitychange", handleVisibilityChange);
    runPoll();

    return () => {
      isActive = false;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      clearScheduledPoll();
    };
  }, [client, runId]);

  const retry = (): void => {
    setError(null);
    pollNowRef.current?.();
  };

  return { projection, error, retry };
}
