import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { TauriEngineClient } from "./EngineInformation/Services/TauriEngineClient";

const rootElement = document.getElementById("root");

if (rootElement === null) {
  throw new Error("The React root element is missing.");
}

const engineClient = new TauriEngineClient();

createRoot(rootElement).render(
  <StrictMode>
    <App engineClient={engineClient} />
  </StrictMode>,
);
