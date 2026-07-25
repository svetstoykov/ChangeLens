import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { TauriEngineStatusClient } from "./EngineStatus/Services/TauriEngineStatusClient";
import { TauriRepositoryClient } from "./Repositories/Services/TauriRepositoryClient";
import { TauriRepositoryFolderPicker } from "./Repositories/Services/TauriRepositoryFolderPicker";
import { TauriComparisonClient } from "./Comparisons/Services/TauriComparisonClient";
import { initializeColorTheme } from "./Visuals/Services/colorThemePreference";
import "@fontsource/ibm-plex-sans/400.css";
import "@fontsource/ibm-plex-sans/600.css";
import "@fontsource/ibm-plex-sans/700.css";
import "@fontsource/ibm-plex-mono/400.css";
import "@fontsource/ibm-plex-mono/600.css";

initializeColorTheme();

const rootElement = document.getElementById("root");

if (rootElement === null) {
  throw new Error("The React root element is missing.");
}

const engineStatusClient = new TauriEngineStatusClient();
const repositoryClient = new TauriRepositoryClient();
const repositoryFolderPicker = new TauriRepositoryFolderPicker();
const comparisonClient = new TauriComparisonClient();

createRoot(rootElement).render(
  <StrictMode>
    <App
      engineStatusClient={engineStatusClient}
      repositoryClient={repositoryClient}
      repositoryFolderPicker={repositoryFolderPicker}
      comparisonClient={comparisonClient}
    />
  </StrictMode>,
);
