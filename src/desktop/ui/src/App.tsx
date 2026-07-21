import { useEffect, useState, type ReactNode } from "react";
import type { ActionError } from "./Actions/Models/ActionError";
import { normalizeActionError } from "./Actions/Services/normalizeActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
import type { EngineClient } from "./EngineInformation/Interfaces/EngineClient";
import type { EngineInformation } from "./EngineInformation/Models/EngineInformation";
import "./styles.css";

interface AppProps {
  engineClient: EngineClient;
}

type EngineState =
  | { status: "connecting" }
  | { status: "ready"; information: EngineInformation }
  | { status: "error"; error: ActionError };

export function App({ engineClient }: AppProps) {
  const [engineState, setEngineState] = useState<EngineState>({
    status: "connecting",
  });

  useEffect(() => {
    let isCurrent = true;

    engineClient
      .getInformation()
      .then((information) => {
        if (isCurrent) {
          setEngineState({ status: "ready", information });
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
  }, [engineClient]);

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
    content = "Connecting to ChangeLens.Engine…";
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
    content = (
      <>
        {state.information.name} {state.information.version} · protocol v
        {state.information.protocolVersion}
      </>
    );
  }

  return (
    <div className="engine-status" data-state={state.status} role="status">
      {content}
    </div>
  );
}
