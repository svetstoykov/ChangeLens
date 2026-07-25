import "@testing-library/jest-dom/vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RepositoryWorkspace } from "../../../../src/desktop/ui/src/Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "../../../../src/desktop/ui/src/Comparisons/Interfaces/ComparisonClient";
import type { ComparisonTarget } from "../../../../src/desktop/ui/src/Comparisons/Models/ComparisonTarget";
import type { ComparisonTargetPage } from "../../../../src/desktop/ui/src/Comparisons/Models/ComparisonTargetPage";
import type { PreparedComparison } from "../../../../src/desktop/ui/src/Comparisons/Models/PreparedComparison";
import type { RepositoryDescriptor } from "../../../../src/desktop/ui/src/Repositories/Models/RepositoryDescriptor";
import { createResolvablePromise } from "../Support/createResolvablePromise";

afterEach(() => {
  vi.useRealTimers();
  cleanup();
});

const repository: RepositoryDescriptor = {
  name: "ChangeLens",
  canonicalPath: "/repo",
  head: { kind: "branch", name: "feature", revision: "a".repeat(40) },
};
const targetA: ComparisonTarget = {
  kind: "local",
  name: "main",
  fullName: "refs/heads/main",
  revision: "b".repeat(40),
};
const targetB: ComparisonTarget = {
  kind: "local",
  name: "release",
  fullName: "refs/heads/release",
  revision: "c".repeat(40),
};
const targetC: ComparisonTarget = {
  kind: "remoteTracking",
  name: "origin/topic",
  fullName: "refs/remotes/origin/topic",
  revision: "d".repeat(40),
};

describe("comparison discovery and freshness orchestration", () => {
  it("marks retained facts stale while preparing a new target and does not freshness-check a mismatched target/token pair", async () => {
    const user = userEvent.setup();
    const targetBPreparation = createResolvablePromise<PreparedComparison>();
    const client = clientWith({
      listTargets: vi.fn().mockResolvedValue(
        targetPage([targetA, targetB], {
          suggestedTarget: targetA,
        }),
      ),
      prepare: vi
        .fn()
        .mockResolvedValueOnce(prepared(targetA, 2))
        .mockImplementationOnce(() => targetBPreparation.promise),
    });
    renderWorkspace(client);
    await screen.findByText("Ready to analyze");

    await selectTarget(user, "release");

    expect(screen.getByText("Preparing comparison…")).toBeVisible();
    expect(
      within(
        screen.getByRole("region", { name: "Comparison freshness" }),
      ).getByText("Freshness: Stale"),
    ).toBeVisible();
    expect(fact("Current work commits")).toHaveTextContent("2");
    expect(screen.queryByText("Ready to analyze")).not.toBeInTheDocument();

    act(() => window.dispatchEvent(new Event("focus")));
    expect(client.checkFreshness).not.toHaveBeenCalled();

    await act(async () => {
      targetBPreparation.resolve(prepared(targetB, 8));
    });

    expect(await screen.findByText("Ready to analyze")).toBeInTheDocument();
    expect(fact("Current work commits")).toHaveTextContent("8");
    expect(
      within(
        screen.getByRole("region", { name: "Comparison freshness" }),
      ).getByText("Freshness: Current"),
    ).toBeVisible();
  });

  it("lets preparation finish when debounced discovery starts later", async () => {
    const user = userEvent.setup();
    const targetBPreparation = createResolvablePromise<PreparedComparison>();
    const queryPage = createResolvablePromise<ComparisonTargetPage>();
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValueOnce(
          targetPage([targetA, targetB], { suggestedTarget: targetA }),
        )
        .mockImplementationOnce(() => queryPage.promise),
      prepare: vi
        .fn()
        .mockResolvedValueOnce(prepared(targetA, 2))
        .mockImplementationOnce(() => targetBPreparation.promise),
    });
    renderWorkspace(client);
    await screen.findByText("Ready to analyze");
    await selectTarget(user, "release");
    vi.useFakeTimers();

    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "topic" },
    });
    await act(async () => vi.advanceTimersByTime(250));
    expect(client.listTargets).toHaveBeenCalledTimes(2);

    await act(async () => {
      targetBPreparation.resolve(prepared(targetB, 8));
    });

    expect(screen.getByText("Ready to analyze")).toBeInTheDocument();
    expect(fact("Current work commits")).toHaveTextContent("8");

    await act(async () => {
      queryPage.resolve(targetPage([targetC]));
    });
  });

  it("invalidates a pending page immediately when the query changes", async () => {
    const user = userEvent.setup();
    const latePage = createResolvablePromise<ComparisonTargetPage>();
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValueOnce(
          targetPage([targetA], {
            nextCursor: "cursor-a",
            targetSetToken: "token-a",
          }),
        )
        .mockImplementationOnce(() => latePage.promise)
        .mockResolvedValueOnce(targetPage([targetC])),
    });
    renderWorkspace(client);
    const input = await openTargets(user);
    await user.click(screen.getByRole("button", { name: "Load more targets" }));
    vi.useFakeTimers();

    fireEvent.change(input, { target: { value: "topic" } });

    expect(screen.queryByRole("option", { name: "main" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Load more targets" }),
    ).toBeNull();

    await act(async () => {
      latePage.resolve(
        targetPage([targetB], {
          targetSetToken: "token-a",
        }),
      );
    });
    expect(screen.queryByRole("option", { name: "release" })).toBeNull();

    await act(async () => vi.advanceTimersByTime(249));
    expect(client.listTargets).toHaveBeenCalledTimes(2);
    await act(async () => vi.advanceTimersByTime(1));

    expect(client.listTargets).toHaveBeenLastCalledWith({
      path: "/repo",
      query: "topic",
    });
    expect(
      screen.getByRole("option", { name: "origin/topic" }),
    ).toBeInTheDocument();
  });

  it("suppresses a late debounced search result after the next query edit", async () => {
    const user = userEvent.setup();
    const firstSearch = createResolvablePromise<ComparisonTargetPage>();
    const secondSearch = createResolvablePromise<ComparisonTargetPage>();
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValueOnce(targetPage([targetA]))
        .mockImplementationOnce(() => firstSearch.promise)
        .mockImplementationOnce(() => secondSearch.promise),
    });
    renderWorkspace(client);
    const input = await openTargets(user);
    vi.useFakeTimers();

    fireEvent.change(input, { target: { value: "first" } });
    await act(async () => vi.advanceTimersByTime(250));
    fireEvent.change(input, { target: { value: "second" } });
    await act(async () => {
      firstSearch.resolve(targetPage([targetB]));
    });

    expect(screen.queryByRole("option", { name: "release" })).toBeNull();

    await act(async () => vi.advanceTimersByTime(250));
    await act(async () => {
      secondSearch.resolve(targetPage([targetC]));
    });

    expect(
      screen.getByRole("option", { name: "origin/topic" }),
    ).toBeInTheDocument();
    expect(client.listTargets).toHaveBeenLastCalledWith({
      path: "/repo",
      query: "second",
    });
  });

  it.each([
    ["comparison.invalidTargetPage", "Validation"],
    ["comparison.targetsChanged", "Conflict"],
  ] as const)(
    "restarts one explicit continuation from page one for %s",
    async (code, type) => {
      const user = userEvent.setup();
      const restartedPage = createResolvablePromise<ComparisonTargetPage>();
      const client = clientWith({
        listTargets: vi
          .fn()
          .mockResolvedValueOnce(
            targetPage([targetA], {
              nextCursor: "cursor-a",
              targetSetToken: "token-a",
            }),
          )
          .mockRejectedValueOnce(actionFailure(code, type))
          .mockImplementationOnce(() => restartedPage.promise),
      });
      renderWorkspace(client);
      await openTargets(user);

      await user.click(
        screen.getByRole("button", { name: "Load more targets" }),
      );

      await waitFor(() => expect(client.listTargets).toHaveBeenCalledTimes(3));
      expect(client.listTargets).toHaveBeenLastCalledWith({ path: "/repo" });
      expect(screen.queryByRole("option", { name: "main" })).toBeNull();

      await act(async () => {
        restartedPage.resolve(targetPage([targetC]));
      });

      expect(
        await screen.findByRole("option", { name: "origin/topic" }),
      ).toBeInTheDocument();
      expect(client.listTargets).toHaveBeenCalledTimes(3);
    },
  );

  it("preserves selection and summary after an invalid query until Reset search is explicit", async () => {
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValueOnce(
          targetPage([targetA], { suggestedTarget: targetA }),
        )
        .mockRejectedValueOnce(
          actionFailure("comparison.invalidTargetQuery", "Validation"),
        )
        .mockResolvedValueOnce(
          targetPage([targetA], { suggestedTarget: targetA }),
        ),
      prepare: vi.fn().mockResolvedValue(prepared(targetA, 2)),
    });
    renderWorkspace(client);
    await screen.findByText("Ready to analyze");
    vi.useFakeTimers();

    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "invalid query" },
    });
    await act(async () => vi.advanceTimersByTime(250));
    await act(async () => undefined);

    expect(screen.getByText("Ready to analyze")).toBeInTheDocument();
    expect(screen.getByText("Selected:")).toHaveTextContent("main");
    expect(client.listTargets).toHaveBeenCalledTimes(2);

    fireEvent.click(screen.getByRole("button", { name: "Reset search" }));

    expect(client.listTargets).toHaveBeenCalledTimes(3);
    expect(client.listTargets).toHaveBeenLastCalledWith({ path: "/repo" });
    expect(screen.getByRole("combobox")).toHaveValue("");
  });

  it.each([
    ["comparison.timedOut", "Timeout"],
    ["comparison.inspectionFailed", "ExternalDependencyFailure"],
  ] as const)(
    "offers an explicit initial retry for %s without a selected target",
    async (code, type) => {
      const user = userEvent.setup();
      const client = clientWith({
        listTargets: vi
          .fn()
          .mockRejectedValueOnce(actionFailure(code, type))
          .mockResolvedValueOnce(targetPage([targetA])),
      });
      renderWorkspace(client);

      const retry = await screen.findByRole("button", {
        name: "Retry loading targets",
      });
      expect(client.listTargets).toHaveBeenCalledOnce();
      expect(
        screen.queryByRole("region", { name: "Comparison freshness" }),
      ).toBeNull();

      await user.click(retry);

      expect(client.listTargets).toHaveBeenCalledTimes(2);
      await openTargets(user);
      expect(screen.getByRole("option", { name: "main" })).toBeInTheDocument();
    },
  );

  it("preserves a safe prior summary and requires an explicit preparation retry", async () => {
    const user = userEvent.setup();
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValue(
          targetPage([targetA, targetB], { suggestedTarget: targetA }),
        ),
      prepare: vi
        .fn()
        .mockResolvedValueOnce(prepared(targetA, 2))
        .mockRejectedValueOnce(actionFailure("comparison.timedOut", "Timeout"))
        .mockResolvedValueOnce(prepared(targetB, 8)),
    });
    renderWorkspace(client);
    await screen.findByText("Ready to analyze");

    await selectTarget(user, "release");

    const retry = await screen.findByRole("button", {
      name: "Retry preparation",
    });
    expect(fact("Current work commits")).toHaveTextContent("2");
    expect(screen.queryByText("Ready to analyze")).toBeNull();
    expect(screen.getByText("Selected:")).toHaveTextContent("release");
    expect(client.prepare).toHaveBeenCalledTimes(2);

    await user.click(retry);

    expect(client.prepare).toHaveBeenCalledTimes(3);
    expect(await screen.findByText("Ready to analyze")).toBeInTheDocument();
    expect(fact("Current work commits")).toHaveTextContent("8");
  });

  it("does not let a focus freshness check invalidate an explicit refresh", async () => {
    const user = userEvent.setup();
    const refreshedTargets = createResolvablePromise<ComparisonTargetPage>();
    const client = clientWith({
      listTargets: vi
        .fn()
        .mockResolvedValueOnce(
          targetPage([targetA], { suggestedTarget: targetA }),
        )
        .mockImplementationOnce(() => refreshedTargets.promise),
      prepare: vi
        .fn()
        .mockResolvedValueOnce(prepared(targetA, 2))
        .mockResolvedValueOnce(prepared(targetA, 9)),
      checkFreshness: vi.fn().mockResolvedValue({ state: "current" }),
    });
    renderWorkspace(client);
    await screen.findByText("Ready to analyze");

    await user.click(
      screen.getByRole("button", { name: "Refresh comparison" }),
    );
    act(() => window.dispatchEvent(new Event("focus")));
    await act(async () => {
      refreshedTargets.resolve(
        targetPage([targetA], { suggestedTarget: targetA }),
      );
    });

    expect(await screen.findByText("Ready to analyze")).toBeInTheDocument();
    expect(fact("Current work commits")).toHaveTextContent("9");
    expect(client.prepare).toHaveBeenCalledTimes(2);
  });
});

function renderWorkspace(client: ComparisonClient) {
  return render(
    <RepositoryWorkspace
      repository={repository}
      comparisonClient={client}
      onRepositoryRefreshed={vi.fn()}
    />,
  );
}

async function openTargets(user: ReturnType<typeof userEvent.setup>) {
  const input = screen.getByRole("combobox", { name: "Find a target" });
  await user.click(input);
  await waitFor(() =>
    expect(screen.queryByText("Loading comparison targets…")).toBeNull(),
  );
  return input;
}

async function selectTarget(
  user: ReturnType<typeof userEvent.setup>,
  name: string,
) {
  await openTargets(user);
  await user.click(screen.getByRole("option", { name }));
}

function fact(label: string): HTMLElement {
  const term = screen.getByText(label);
  const container = term.parentElement;
  expect(container).not.toBeNull();
  return container!;
}

function clientWith(
  overrides: Partial<ComparisonClient> = {},
): ComparisonClient {
  return {
    listTargets: vi.fn().mockResolvedValue(targetPage([])),
    prepare: vi.fn(),
    checkFreshness: vi.fn().mockResolvedValue({ state: "current" }),
    ...overrides,
  };
}

function targetPage(
  targets: readonly ComparisonTarget[],
  overrides: Partial<ComparisonTargetPage> = {},
): ComparisonTargetPage {
  return {
    targets,
    suggestedTarget: null,
    nextCursor: null,
    targetSetToken: "e".repeat(64),
    unsupportedTargetCount: 0,
    ...overrides,
  };
}

function prepared(
  selectedTarget: ComparisonTarget,
  currentWorkCommitCount: number,
): PreparedComparison {
  return {
    repository,
    target: selectedTarget,
    mergeBaseRevision: "f".repeat(40),
    currentWorkCommitCount,
    targetOnlyCommitCount: 1,
    changedFileTotal: currentWorkCommitCount + 1,
    uncommittedFileTotal: 1,
    stagedFileCount: 1,
    unstagedFileCount: 0,
    untrackedFileCount: 0,
    readiness: { state: "ready" },
    freshnessToken: selectedTarget.fullName.includes("release")
      ? "1".repeat(64)
      : "2".repeat(64),
  };
}

function actionFailure(code: string, type: string) {
  return {
    kind: "operation",
    errors: [{ code, type, message: `${code} failure.` }],
  };
}
