import type { ReactNode } from "react";
import { LocalIcon } from "./Visuals/Components/LocalIcon";
import changelensMark from "./assets/changelens-mark.svg";
import fileIcon from "./assets/file.svg";

interface AppShellProps {
  readonly repositoryIdentity?: ReactNode;
  readonly onOpenAnotherRepository?: () => void;
  readonly children?: ReactNode;
}

export function AppShell({
  repositoryIdentity,
  onOpenAnotherRepository,
  children,
}: AppShellProps) {
  return (
    <div className="app-shell">
      <aside className="app-rail">
        <div className="brand">
          <LocalIcon className="brand-mark" source={changelensMark} />
          <h1>ChangeLens</h1>
        </div>
        {repositoryIdentity ? (
          <nav aria-label="ChangeLens workspace">
            <span className="current-navigation-item" aria-current="page">
              <LocalIcon source={fileIcon} />
              Current change
            </span>
          </nav>
        ) : null}
        {repositoryIdentity}
        {onOpenAnotherRepository ? (
          <button
            className="open-repository-button"
            type="button"
            onClick={onOpenAnotherRepository}
          >
            Open another repository
          </button>
        ) : null}
      </aside>
      <header className="technical-bar">
        <span>
          {repositoryIdentity ? "Local repository" : "Local workspace"}
        </span>
      </header>
      <main className="app-main">{children}</main>
    </div>
  );
}
