import { useEffect, useState, type ReactNode } from "react";
import type { EngineClient } from "./EngineInformation/Interfaces/EngineClient";
import type { EngineInformation } from "./EngineInformation/Models/EngineInformation";
import "./styles.css";

interface AppProps {
  engineClient: EngineClient;
}

type EngineState =
  | { status: "connecting" }
  | { status: "ready"; information: EngineInformation }
  | { status: "error" };

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
      .catch(() => {
        if (isCurrent) {
          setEngineState({ status: "error" });
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
    content = "Desktop engine unavailable";
  } else {
    content = (
      <>
        {state.information.name} {state.information.version} · protocol v
        {state.information.protocolVersion}
      </>
    );
  }

  return (
    <p className="engine-status" data-state={state.status} role="status">
      {content}
    </p>
  );
}
