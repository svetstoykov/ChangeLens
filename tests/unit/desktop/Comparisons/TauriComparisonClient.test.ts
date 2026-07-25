import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { invoke } from "@tauri-apps/api/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TauriComparisonClient } from "../../../../src/desktop/ui/src/Comparisons/Services/TauriComparisonClient";

vi.mock("@tauri-apps/api/core", () => ({ invoke: vi.fn() }));

describe("TauriComparisonClient", () => {
  beforeEach(() => vi.mocked(invoke).mockReset());

  it("uses only the three explicit comparison Tauri commands", async () => {
    vi.mocked(invoke)
      .mockResolvedValueOnce({
        targets: [],
        suggestedTarget: null,
        nextCursor: null,
        targetSetToken: "a".repeat(64),
        unsupportedTargetCount: 0,
      })
      .mockResolvedValueOnce({})
      .mockResolvedValueOnce({ state: "current" });
    const client = new TauriComparisonClient();

    await client.listTargets({ path: "/repo" });
    await client.prepare({ path: "/repo", target: "refs/heads/main" });
    await client.checkFreshness({
      path: "/repo",
      target: "refs/heads/main",
      freshnessToken: "a".repeat(64),
    });

    expect(invoke).toHaveBeenNthCalledWith(1, "comparison_list_targets", {
      path: "/repo",
    });
    expect(invoke).toHaveBeenNthCalledWith(2, "comparison_prepare", {
      path: "/repo",
      target: "refs/heads/main",
    });
    expect(invoke).toHaveBeenNthCalledWith(3, "comparison_check_freshness", {
      path: "/repo",
      target: "refs/heads/main",
      freshnessToken: "a".repeat(64),
    });
  });

  it("omits absent optional list arguments", async () => {
    vi.mocked(invoke).mockResolvedValue({
      targets: [],
      suggestedTarget: null,
      nextCursor: null,
      targetSetToken: "a".repeat(64),
      unsupportedTargetCount: 0,
    });
    const client = new TauriComparisonClient();

    await client.listTargets({ path: "/repo" });
    expect(invoke).toHaveBeenCalledWith("comparison_list_targets", {
      path: "/repo",
    });
  });

  it("keeps protocol metadata and dotted action identifiers out of React", () => {
    const source = readFileSync(
      resolve(
        process.cwd(),
        "src/Comparisons/Services/TauriComparisonClient.ts",
      ),
      "utf8",
    );
    expect(source).not.toContain("protocolVersion");
    expect(source).not.toContain("requestId");
    expect(source).not.toContain("comparisons.listTargets");
  });
});
