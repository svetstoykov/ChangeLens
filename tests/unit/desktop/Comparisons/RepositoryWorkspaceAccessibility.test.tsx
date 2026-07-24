import "@testing-library/jest-dom/vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RepositoryWorkspace } from "../../../../src/desktop/ui/src/Comparisons/Components/RepositoryWorkspace";
import type { ComparisonClient } from "../../../../src/desktop/ui/src/Comparisons/Interfaces/ComparisonClient";
import type { RepositoryDescriptor } from "../../../../src/desktop/ui/src/Repositories/Models/RepositoryDescriptor";

afterEach(cleanup);

const longPath = `/workspace/${"nested-segment/".repeat(12)}change_lens`;
const longBranch = `feature/${"long-branch-segment-".repeat(8)}`;
const longRevision = "0123456789abcdef".repeat(4);
const repository: RepositoryDescriptor = {
  name: "ChangeLens",
  canonicalPath: longPath,
  head: {
    kind: "branch",
    name: longBranch,
    revision: longRevision,
  },
};

describe("RepositoryWorkspace accessibility", () => {
  it("uses one semantic setup region and one facts region without duplicating content", async () => {
    renderWorkspace();

    const setup = await screen.findByRole("region", {
      name: "Comparison setup",
    });
    const facts = screen.getByRole("region", {
      name: "Current change facts",
    });

    expect(
      screen.getAllByRole("region", { name: "Comparison setup" }),
    ).toHaveLength(1);
    expect(
      screen.getAllByRole("region", { name: "Current change facts" }),
    ).toHaveLength(1);
    expect(within(setup).getByText("Change context")).toBeInTheDocument();
    expect(within(facts).getByText("Changed files")).toBeInTheDocument();
    expect(screen.getAllByText("Changed files")).toHaveLength(1);
  });

  it("presents freshness and readiness with visible text, icons, and non-color state", async () => {
    renderWorkspace();

    const readiness = await screen.findByText("Ready to analyze");
    expect(readiness.closest("[data-readiness]")).toHaveAttribute(
      "data-readiness",
      "ready",
    );

    const freshness = screen.getByRole("region", {
      name: "Comparison freshness",
    });
    expect(within(freshness).getByText("Freshness: Current")).toBeVisible();
    expect(
      within(freshness).getByRole("button", { name: "Refresh comparison" }),
    ).not.toHaveAttribute("title");
    expect(
      freshness.querySelector('.local-icon[aria-hidden="true"]'),
    ).not.toBeNull();
  });

  it("keeps long repository values available to assistive technology", async () => {
    renderWorkspace();
    await screen.findByText("Ready to analyze");

    for (const value of [longPath, longBranch, longRevision]) {
      const element = screen.getByText(value);
      expect(element).not.toHaveAttribute("aria-hidden");
      expect(element.closest('[aria-hidden="true"]')).toBeNull();
    }
  });

  it("defines the local responsive visual and motion contract", () => {
    const sourceRoot = resolve(process.cwd(), "src");
    const styles = readFileSync(resolve(sourceRoot, "styles.css"), "utf8");
    const mainSource = readFileSync(resolve(sourceRoot, "main.tsx"), "utf8");
    const componentSources = [
      "AppShell.tsx",
      "Repositories/Components/RepositoryIdentity.tsx",
      "Comparisons/Components/RepositoryWorkspace.tsx",
      "Comparisons/Components/TargetCombobox.tsx",
      "Comparisons/Components/ComparisonSummary.tsx",
      "Comparisons/Components/FreshnessControl.tsx",
    ]
      .map((path) => readFileSync(resolve(sourceRoot, path), "utf8"))
      .join("\n");

    expect(mainSource).toContain("@fontsource/ibm-plex-sans/400.css");
    expect(mainSource).toContain("@fontsource/ibm-plex-sans/600.css");
    expect(mainSource).toContain("@fontsource/ibm-plex-sans/700.css");
    expect(mainSource).toContain("@fontsource/ibm-plex-mono/400.css");
    expect(mainSource).toContain("@fontsource/ibm-plex-mono/600.css");
    expect(styles).toMatch(/:focus-visible/);
    expect(styles).toMatch(/prefers-color-scheme:\s*dark/);
    expect(styles).toMatch(/prefers-reduced-motion:\s*reduce/);
    expect(styles).toMatch(/overflow-wrap:\s*anywhere/);
    expect(styles).toMatch(/max-width:\s*960px/);
    expect(styles).toMatch(/IBM Plex Sans/);
    expect(styles).toMatch(/IBM Plex Mono/);
    expect(componentSources).toMatch(/assets\/[a-z-]+\.svg/);
    expect(styles).not.toMatch(/filter:\s*invert/);
    expect(styles).not.toMatch(
      /(?:gap|padding(?:-\w+)?|margin(?:-\w+)?):[^;]*(?:^|\s)(?:6|10|12|20)px/,
    );
    expect(`${styles}\n${mainSource}\n${componentSources}`).not.toMatch(
      /fonts\.googleapis|fonts\.gstatic|Material Symbols|cdn\.tailwindcss|https?:\/\//,
    );
  });

  it("keeps controls and normal-sized state text above required contrast", () => {
    const styles = readFileSync(
      resolve(process.cwd(), "src", "styles.css"),
      "utf8",
    );
    const lightTheme = getRequiredMatch(styles, /^:root\s*\{([\s\S]*?)^\}/m);
    const darkTheme = getRequiredMatch(
      styles,
      /@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([\s\S]*?)^\s{2}\}/m,
    );

    for (const theme of [lightTheme, darkTheme]) {
      expect(
        contrastRatio(
          cssColor(theme, "border-control"),
          cssColor(theme, "surface-elevated"),
        ),
      ).toBeGreaterThanOrEqual(3);
      expect(
        contrastRatio(
          cssColor(theme, "on-primary"),
          cssColor(theme, "primary-action"),
        ),
      ).toBeGreaterThanOrEqual(4.5);
      expect(
        contrastRatio(
          cssColor(theme, "on-primary"),
          cssColor(theme, "primary-action-hover"),
        ),
      ).toBeGreaterThanOrEqual(4.5);
    }

    expect(styles).toMatch(
      /\.current-navigation-item\s*\{[\s\S]*?color:\s*var\(--text-primary\)/,
    );
    expect(styles).toMatch(
      /\.readiness-status\s*\{[\s\S]*?color:\s*var\(--text-primary\)/,
    );
    expect(styles).toMatch(
      /\.freshness-control p\s*\{[\s\S]*?color:\s*var\(--text-primary\)/,
    );
    expect(styles).toMatch(
      /\.action-alert,\s*\[role="alert"\]\s*\{[\s\S]*?color:\s*var\(--text-primary\)/,
    );
  });
});

function renderWorkspace() {
  return render(
    <RepositoryWorkspace
      repository={repository}
      comparisonClient={comparisonClient()}
      onRepositoryRefreshed={vi.fn()}
    />,
  );
}

function comparisonClient(): ComparisonClient {
  const target = {
    kind: "local" as const,
    name: "main",
    fullName: "refs/heads/main",
    revision: "b".repeat(40),
  };

  return {
    listTargets: vi.fn().mockResolvedValue({
      targets: [target],
      suggestedTarget: target,
      nextCursor: null,
      targetSetToken: "c".repeat(64),
      unsupportedTargetCount: 0,
    }),
    prepare: vi.fn().mockResolvedValue({
      repository,
      target,
      mergeBaseRevision: "d".repeat(40),
      currentWorkCommitCount: 2,
      targetOnlyCommitCount: 1,
      changedFileTotal: 3,
      uncommittedFileTotal: 1,
      stagedFileCount: 1,
      unstagedFileCount: 1,
      untrackedFileCount: 0,
      readiness: { state: "ready" as const },
      freshnessToken: "e".repeat(64),
    }),
    checkFreshness: vi.fn().mockResolvedValue({ state: "current" as const }),
  };
}

function getRequiredMatch(value: string, pattern: RegExp): string {
  const match = pattern.exec(value);
  expect(match).not.toBeNull();
  return match?.[1] ?? "";
}

function cssColor(theme: string, name: string): string {
  const match = new RegExp(`--${name}:\\s*(#[0-9a-f]{6})`, "i").exec(theme);
  expect(match).not.toBeNull();
  return match?.[1] ?? "#000000";
}

function contrastRatio(first: string, second: string): number {
  const lighter = Math.max(relativeLuminance(first), relativeLuminance(second));
  const darker = Math.min(relativeLuminance(first), relativeLuminance(second));
  return (lighter + 0.05) / (darker + 0.05);
}

function relativeLuminance(color: string): number {
  const channels = color
    .slice(1)
    .match(/.{2}/g)!
    .map((channel) => Number.parseInt(channel, 16) / 255)
    .map((channel) =>
      channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
    );
  return 0.2126 * channels[0]! + 0.7152 * channels[1]! + 0.0722 * channels[2]!;
}
