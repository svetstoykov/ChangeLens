import { useEffect, useState, type ReactNode } from "react";
import type { ActionError } from "./Actions/Models/ActionError";
import { normalizeActionError } from "./Actions/Services/normalizeActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
import type { EngineStatusClient } from "./EngineStatus/Interfaces/EngineStatusClient";
import "./styles.css";

interface AppProps {
  engineStatusClient: EngineStatusClient;
}

type EngineState =
  | { status: "connecting" }
  | { status: "ready" }
  | { status: "error"; error: ActionError };

export function App({ engineStatusClient }: AppProps) {
  const [engineState, setEngineState] = useState<EngineState>({
    status: "connecting",
  });

  useEffect(() => {
    let isCurrent = true;

    engineStatusClient
      .checkStatus()
      .then(() => {
        if (isCurrent) {
          setEngineState({ status: "ready" });
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setEngineState({
            status: "error",
            error: normalizeActionError(error),
          });
        }
      });

    return () => {
      isCurrent = false;
    };
  }, [engineStatusClient]);

  return (
    <main className="app-shell">
      <h1>ChangeLens</h1>
      <p>Desktop UI infrastructure</p>
      <EngineStatus state={engineState} />
    </main>
  );
}

interface EngineStatusProps {
  state: EngineState;
}

function EngineStatus({ state }: EngineStatusProps) {
  let content: ReactNode;

  if (state.status === "connecting") {
    content = "Connecting to the ChangeLens engine…";
  } else if (state.status === "error") {
    const presentation = presentActionError(state.error, {
      "engine.responseTimedOut": "Engine response timed out",
    });

    content = (
      <>
        <strong>{presentation.title}</strong>
        <ul className="action-errors">
          {presentation.messages.map((message, index) => (
            <li key={`${state.error.errors[index]!.code}-${index}`}>
              {message}
            </li>
          ))}
        </ul>
        {presentation.requestId ? (
          <small>Request {presentation.requestId}</small>
        ) : null}
      </>
    );
  } else {
    content = "The ChangeLens engine is ready.";
  }

  return (
    <div className="engine-status" data-state={state.status} role="status">
      {content}
    </div>
  );
}
