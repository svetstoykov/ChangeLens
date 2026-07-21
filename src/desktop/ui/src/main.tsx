import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { TauriEngineStatusClient } from "./EngineStatus/Services/TauriEngineStatusClient";

const rootElement = document.getElementById("root");

if (rootElement === null) {
  throw new Error("The React root element is missing.");
}

const engineStatusClient = new TauriEngineStatusClient();

createRoot(rootElement).render(
  <StrictMode>
    <App engineStatusClient={engineStatusClient} />
  </StrictMode>,
);
