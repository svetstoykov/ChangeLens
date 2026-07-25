import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ComparisonSummary } from "../../../../src/desktop/ui/src/Comparisons/Components/ComparisonSummary";
import type { PreparedComparison } from "../../../../src/desktop/ui/src/Comparisons/Models/PreparedComparison";

afterEach(cleanup);

describe("ComparisonSummary", () => {
  it("shows the conflicted-file count with the other aggregate facts", () => {
    render(
      <ComparisonSummary
        preparedComparison={preparedComparison}
        freshness="current"
      />,
    );

    expect(
      screen.getByText("Resolve conflicts outside ChangeLens, then refresh"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Conflicted files").parentElement,
    ).toHaveTextContent("4");
    expect(screen.getByText("Changed files").parentElement).toHaveTextContent(
      "7",
    );
    expect(screen.queryByText("Ready to analyze")).not.toBeInTheDocument();
  });
});

const preparedComparison: PreparedComparison = {
  repository: {
    name: "ChangeLens",
    canonicalPath: "/repo",
    head: { kind: "branch", name: "feature", revision: "a".repeat(40) },
  },
  target: {
    kind: "local",
    name: "main",
    fullName: "refs/heads/main",
    revision: "b".repeat(40),
  },
  mergeBaseRevision: "c".repeat(40),
  currentWorkCommitCount: 2,
  targetOnlyCommitCount: 1,
  changedFileTotal: 7,
  uncommittedFileTotal: 4,
  stagedFileCount: 1,
  unstagedFileCount: 2,
  untrackedFileCount: 1,
  readiness: { state: "conflicts", conflictedFileCount: 4 },
  freshnessToken: "d".repeat(64),
};
