import type { ReactNode } from "react";
import { Icon } from "./Visuals/Components/Icon";
import type { ColorTheme } from "./Visuals/Models/ColorTheme";

interface AppShellProps {
  readonly hasRepository?: boolean;
  readonly onOpenAnotherRepository?: () => void;
  readonly onShowRepositories?: () => void;
  readonly onShowCurrentChange?: () => void;
  readonly currentDestination?: "change" | "repositories";
  readonly showRepositoryNavigation?: boolean;
  readonly colorTheme: ColorTheme;
  readonly onToggleColorTheme: () => void;
  readonly children?: ReactNode;
}

export function AppShell({
  hasRepository = false,
  onOpenAnotherRepository,
  onShowRepositories,
  onShowCurrentChange,
  currentDestination = "change",
  showRepositoryNavigation = hasRepository,
  colorTheme,
  onToggleColorTheme,
  children,
}: AppShellProps) {
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
        {showRepositoryNavigation ? (
          <nav aria-label="ChangeLens workspace">
            {hasRepository ? (
              <button
                className="current-navigation-item"
                aria-current={
                  currentDestination === "change" ? "page" : undefined
                }
                type="button"
                onClick={onShowCurrentChange}
              >
                <span className="navigation-icon">
                  <Icon name="currentChange" />
                </span>
                <span>
                  <strong>Current change</strong>
                  <small>Prepare comparison</small>
                </span>
              </button>
            ) : null}
            <button
              className="current-navigation-item"
              aria-current={
                currentDestination === "repositories" ? "page" : undefined
              }
              type="button"
              onClick={onShowRepositories}
            >
              <span className="navigation-icon">
                <Icon name="folder" />
              </span>
              <span>
                <strong>Repositories</strong>
                <small>Recent local worktrees</small>
              </span>
            </button>
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
            onClick={onToggleColorTheme}
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
