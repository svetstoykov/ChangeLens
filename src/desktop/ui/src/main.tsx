import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { TauriEngineStatusClient } from "./EngineStatus/Services/TauriEngineStatusClient";
import { TauriRepositoryClient } from "./Repositories/Services/TauriRepositoryClient";
import { TauriRepositoryHistoryClient } from "./Repositories/Services/TauriRepositoryHistoryClient";
import { TauriRepositoryFolderPicker } from "./Repositories/Services/TauriRepositoryFolderPicker";
import { TauriComparisonClient } from "./Comparisons/Services/TauriComparisonClient";
import { TauriAnalysisClient } from "./Analysis/Services/TauriAnalysisClient";
import { initializeColorTheme } from "./Visuals/Services/colorThemePreference";
import { TauriColorThemePreferenceClient } from "./Visuals/Services/TauriColorThemePreferenceClient";
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
const repositoryHistoryClient = new TauriRepositoryHistoryClient();
const repositoryFolderPicker = new TauriRepositoryFolderPicker();
const comparisonClient = new TauriComparisonClient();
const analysisClient = new TauriAnalysisClient();
const colorThemePreferenceClient = new TauriColorThemePreferenceClient();

createRoot(rootElement).render(
  <StrictMode>
    <App
      engineStatusClient={engineStatusClient}
      repositoryClient={repositoryClient}
      repositoryHistoryClient={repositoryHistoryClient}
      repositoryFolderPicker={repositoryFolderPicker}
      comparisonClient={comparisonClient}
      analysisClient={analysisClient}
      colorThemePreferenceClient={colorThemePreferenceClient}
    />
  </StrictMode>,
);
