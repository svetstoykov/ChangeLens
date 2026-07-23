import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { TauriEngineStatusClient } from "./EngineStatus/Services/TauriEngineStatusClient";
import { TauriRepositoryClient } from "./Repositories/Services/TauriRepositoryClient";
import { TauriRepositoryFolderPicker } from "./Repositories/Services/TauriRepositoryFolderPicker";

const rootElement = document.getElementById("root");

if (rootElement === null) {
  throw new Error("The React root element is missing.");
}

const engineStatusClient = new TauriEngineStatusClient();
const repositoryClient = new TauriRepositoryClient();
const repositoryFolderPicker = new TauriRepositoryFolderPicker();

createRoot(rootElement).render(
  <StrictMode>
    <App
      engineStatusClient={engineStatusClient}
      repositoryClient={repositoryClient}
      repositoryFolderPicker={repositoryFolderPicker}
    />
  </StrictMode>,
);
