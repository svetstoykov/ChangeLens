import { useEffect, useState } from "react";
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
  if (state.status === "connecting") {
    return (
      <p className="engine-status" data-state="connecting">
        Connecting to ChangeLens.Engine…
      </p>
    );
  }

  if (state.status === "error") {
    return (
      <p className="engine-status" data-state="error">
        Desktop engine unavailable
      </p>
    );
  }

  return (
    <p className="engine-status" data-state="ready">
      {state.information.name} {state.information.version} · protocol v
      {state.information.protocolVersion}
    </p>
  );
}
