import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RepositoryWorkspace } from "../../../../src/desktop/ui/src/Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "../../../../src/desktop/ui/src/Comparisons/Interfaces/ComparisonClient";
import type { ComparisonTarget } from "../../../../src/desktop/ui/src/Comparisons/Models/ComparisonTarget";
import type { PreparedComparison } from "../../../../src/desktop/ui/src/Comparisons/Models/PreparedComparison";
import type { RepositoryDescriptor } from "../../../../src/desktop/ui/src/Repositories/Models/RepositoryDescriptor";

afterEach(cleanup);

const repository: RepositoryDescriptor = {
  name: "ChangeLens",
  canonicalPath: "/repo",
  head: { kind: "branch", name: "feature", revision: "a".repeat(40) },
};
const target: ComparisonTarget = {
  kind: "local",
  name: "main",
  fullName: "refs/heads/main",
  revision: "b".repeat(40),
};

describe("RepositoryWorkspace", () => {
  it("prepares the suggested target and presents bounded aggregate facts", async () => {
    const client = comparisonClient();
    const onRepositoryRefreshed = vi.fn();

    render(
      <RepositoryWorkspace
        repository={repository}
        comparisonClient={client}
        onRepositoryRefreshed={onRepositoryRefreshed}
      />,
    );

    expect(await screen.findByText("Ready to analyze")).toBeInTheDocument();
    expect(client.listTargets).toHaveBeenCalledWith({ path: "/repo" });
    expect(client.prepare).toHaveBeenCalledWith({
      path: "/repo",
      target: "refs/heads/main",
    });
    expect(screen.getByText("Changed files")).toBeInTheDocument();
    expect(screen.getByText(/File categories can overlap/)).toBeInTheDocument();
    expect(onRepositoryRefreshed).toHaveBeenCalledWith(repository);
    expect(
      screen.queryByRole("button", { name: /analyze/i }),
    ).not.toBeInTheDocument();
  });

  it("preserves Change context while selecting another exact target and refreshing", async () => {
    const user = userEvent.setup();
    const remoteTarget: ComparisonTarget = {
      kind: "remoteTracking",
      name: "origin/release",
      fullName: "refs/remotes/origin/release",
      revision: "c".repeat(40),
    };
    const client = comparisonClient([target, remoteTarget]);

    render(
      <RepositoryWorkspace
        repository={repository}
        comparisonClient={client}
        onRepositoryRefreshed={vi.fn()}
      />,
    );
    await screen.findByText("Ready to analyze");

    await user.type(
      screen.getByLabelText(
        "Describe the change you want to understand (optional)",
      ),
      "Keep this local context",
    );
    await user.click(screen.getByRole("combobox", { name: "Find a target" }));
    await user.click(screen.getByRole("option", { name: "origin/release" }));
    await waitFor(() =>
      expect(client.prepare).toHaveBeenLastCalledWith({
        path: "/repo",
        target: "refs/remotes/origin/release",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Refresh comparison" }),
    );

    await waitFor(() => expect(client.listTargets).toHaveBeenCalledTimes(2));
    expect(
      screen.getByLabelText(
        "Describe the change you want to understand (optional)",
      ),
    ).toHaveValue("Keep this local context");
  });
});

function comparisonClient(
  targets: readonly ComparisonTarget[] = [target],
): ComparisonClient {
  return {
    listTargets: vi.fn().mockResolvedValue({
      targets,
      suggestedTarget: target,
      nextCursor: null,
      targetSetToken: "d".repeat(64),
      unsupportedTargetCount: 0,
    }),
    prepare: vi
      .fn()
      .mockImplementation(async ({ target: fullName }) =>
        preparedComparison(
          targets.find((item) => item.fullName === fullName) ?? target,
        ),
      ),
    checkFreshness: vi.fn().mockResolvedValue({ state: "current" }),
  };
}

function preparedComparison(
  selectedTarget: ComparisonTarget,
): PreparedComparison {
  return {
    repository,
    target: selectedTarget,
    mergeBaseRevision: "d".repeat(40),
    currentWorkCommitCount: 2,
    targetOnlyCommitCount: 1,
    changedFileTotal: 3,
    uncommittedFileTotal: 1,
    stagedFileCount: 1,
    unstagedFileCount: 1,
    untrackedFileCount: 0,
    readiness: { state: "ready" },
    freshnessToken: "e".repeat(64),
  };
}
