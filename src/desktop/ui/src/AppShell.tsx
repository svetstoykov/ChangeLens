import type { ReactNode } from "react";
import type { ActionError } from "./Actions/Models/ActionError";
import { presentActionError } from "./Actions/Services/presentActionError";
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
  readonly themeError?: ActionError | null;
  readonly onDismissThemeError?: () => void;
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
  themeError = null,
  onDismissThemeError,
  children,
}: AppShellProps) {
  const nextTheme = colorTheme === "light" ? "dark" : "light";
  const themeErrorPresentation =
    themeError !== null ? presentActionError(themeError) : null;

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
          <WorkspaceNavigation
            hasRepository={hasRepository}
            currentDestination={currentDestination}
            onShowCurrentChange={onShowCurrentChange}
            onShowRepositories={onShowRepositories}
          />
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
              <small>Local-first; only filtered context is sent to AI</small>
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
        <div className="mobile-brand">
          <span className="mobile-brand-mark" aria-hidden="true">
            <Icon name="logo" />
          </span>
          <h1>ChangeLens</h1>
        </div>
        <div className="workspace-location">
          <Icon
            name={
              currentDestination === "repositories"
                ? "folder"
                : hasRepository
                  ? "currentChange"
                  : "shield"
            }
          />
          <span>
            {currentDestination === "repositories"
              ? "Repositories"
              : hasRepository
                ? "Current change"
                : "Local workspace"}
          </span>
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
            Local-first
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
      {showRepositoryNavigation ? (
        <WorkspaceNavigation
          className="mobile-navigation"
          hasRepository={hasRepository}
          currentDestination={currentDestination}
          onShowCurrentChange={onShowCurrentChange}
          onShowRepositories={onShowRepositories}
        />
      ) : null}
      <main className="app-main">
        {themeErrorPresentation !== null ? (
          <section className="action-alert theme-alert" role="alert">
            <Icon name="warning" />
            <div>
              <strong>{themeErrorPresentation.title}</strong>
              <ul>
                {themeErrorPresentation.messages.map((message, index) => (
                  <li
                    key={`${themeError?.errors[index]?.code ?? "theme"}-${index}`}
                  >
                    {message}
                  </li>
                ))}
              </ul>
              {onDismissThemeError ? (
                <div className="alert-actions">
                  <button type="button" onClick={onDismissThemeError}>
                    Dismiss
                  </button>
                </div>
              ) : null}
            </div>
          </section>
        ) : null}
        {children}
      </main>
    </div>
  );
}

function WorkspaceNavigation({
  className,
  hasRepository,
  currentDestination,
  onShowCurrentChange,
  onShowRepositories,
}: {
  readonly className?: string;
  readonly hasRepository: boolean;
  readonly currentDestination: "change" | "repositories";
  readonly onShowCurrentChange?: () => void;
  readonly onShowRepositories?: () => void;
}) {
  return (
    <nav className={className} aria-label="ChangeLens workspace">
      {hasRepository ? (
        <button
          className="current-navigation-item"
          aria-current={currentDestination === "change" ? "page" : undefined}
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
  );
}
