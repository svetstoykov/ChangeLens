import type { ReactNode } from "react";
import { Icon } from "./Visuals/Components/Icon";
import { useColorTheme } from "./Visuals/Hooks/useColorTheme";

interface AppShellProps {
  readonly hasRepository?: boolean;
  readonly onOpenAnotherRepository?: () => void;
  readonly children?: ReactNode;
}

export function AppShell({
  hasRepository = false,
  onOpenAnotherRepository,
  children,
}: AppShellProps) {
  const { colorTheme, toggleColorTheme } = useColorTheme();
  const nextTheme = colorTheme === "light" ? "dark" : "light";

  return (
    <div className="app-shell" data-has-repository={hasRepository}>
      <aside className="app-sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <Icon name="logo" />
          </span>
          <div className="brand-copy">
            <h1>ChangeLens</h1>
            <span>Local change intelligence</span>
          </div>
        </div>
        {hasRepository ? (
          <nav aria-label="ChangeLens workspace">
            <span className="current-navigation-item" aria-current="page">
              <span className="navigation-icon">
                <Icon name="currentChange" />
              </span>
              <span>
                <strong>Current change</strong>
                <small>Prepare comparison</small>
              </span>
            </span>
          </nav>
        ) : (
          <p className="sidebar-introduction">
            Understand a repository change with local, evidence-backed analysis.
          </p>
        )}
        <footer className="sidebar-footer">
          <p className="local-assurance">
            <Icon name="shield" />
            <span>
              <strong>Local by design</strong>
              <small>No repository data leaves this device</small>
            </span>
          </p>
          {onOpenAnotherRepository ? (
            <button
              className="open-repository-button"
              type="button"
              onClick={onOpenAnotherRepository}
            >
              <Icon name="folder" />
              Switch repository
            </button>
          ) : null}
        </footer>
      </aside>
      <header className="technical-bar">
        <div className="mobile-brand" aria-label="ChangeLens">
          <span className="mobile-brand-mark">
            <Icon name="logo" />
          </span>
          <strong>ChangeLens</strong>
        </div>
        <div className="workspace-location">
          <Icon name={hasRepository ? "currentChange" : "shield"} />
          <span>{hasRepository ? "Current change" : "Local workspace"}</span>
        </div>
        <div className="technical-actions">
          <button
            className="theme-toggle"
            type="button"
            aria-label={`Switch to ${nextTheme} theme`}
            title={`Switch to ${nextTheme} theme`}
            onClick={toggleColorTheme}
          >
            <Icon name={colorTheme === "light" ? "moon" : "sun"} />
            <span>{nextTheme} theme</span>
          </button>
          <span className="privacy-indicator">
            <span aria-hidden="true" />
            Local only
          </span>
          {onOpenAnotherRepository ? (
            <button
              className="mobile-repository-button"
              type="button"
              onClick={onOpenAnotherRepository}
            >
              <Icon name="folder" />
              <span>Switch repository</span>
            </button>
          ) : null}
        </div>
      </header>
      <main className="app-main">{children}</main>
    </div>
  );
}
