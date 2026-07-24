import type { ReactNode } from "react";

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
    <main className="app-shell">
      <header>
        <h1>ChangeLens</h1>
        {repositoryIdentity}
        {onOpenAnotherRepository ? (
          <button type="button" onClick={onOpenAnotherRepository}>
            Open another repository
          </button>
        ) : null}
      </header>
      {children}
    </main>
  );
}
