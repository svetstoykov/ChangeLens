import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AppShell } from "../../../src/desktop/ui/src/AppShell";
import { RepositoryIdentity } from "../../../src/desktop/ui/src/Repositories/Components/RepositoryIdentity";

afterEach(cleanup);

describe("AppShell", () => {
  it("provides one main landmark and a truthful current-change navigation rail", () => {
    render(
      <AppShell
        repositoryIdentity={
          <RepositoryIdentity
            repository={{
              name: "ChangeLens",
              canonicalPath: "/work/change_lens",
              head: {
                kind: "branch",
                name: "feature/phase-1b",
                revision: "a".repeat(40),
              },
            }}
          />
        }
        onOpenAnotherRepository={vi.fn()}
      >
        <section aria-labelledby="workspace-heading">
          <h2 id="workspace-heading">Prepare the comparison</h2>
        </section>
      </AppShell>,
    );

    expect(screen.getAllByRole("main")).toHaveLength(1);
    const navigation = screen.getByRole("navigation", {
      name: "ChangeLens workspace",
    });
    expect(within(navigation).getByText("Current change")).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(navigation).queryAllByRole("link")).toHaveLength(0);
    expect(
      screen.queryByText(
        /new analysis|repositories|history|settings|documentation|support/i,
      ),
    ).not.toBeInTheDocument();
  });

  it("uses a local brand image and visible names for every control", () => {
    render(
      <AppShell onOpenAnotherRepository={vi.fn()}>
        <p>Workspace</p>
      </AppShell>,
    );

    expect(
      screen.getByRole("heading", { name: "ChangeLens", level: 1 }),
    ).toBeInTheDocument();
    const mark = document.querySelector<HTMLSpanElement>(
      ".local-icon.brand-mark",
    );
    expect(mark).toHaveAttribute("aria-hidden", "true");
    expect(mark?.style.getPropertyValue("--icon-source")).toMatch(
      /^url\("(?:data:image\/svg\+xml|\/.*changelens-mark\.svg)/,
    );
    expect(mark?.style.getPropertyValue("--icon-source")).not.toMatch(
      /https?:/,
    );

    for (const button of screen.getAllByRole("button")) {
      expect(button).toHaveAccessibleName();
      expect(button).not.toHaveAttribute("title");
    }
  });

  it("keeps heading levels in logical order", () => {
    render(
      <AppShell
        repositoryIdentity={
          <RepositoryIdentity
            repository={{
              name: "ChangeLens",
              canonicalPath: "/repo",
              head: { kind: "detached", revision: "b".repeat(40) },
            }}
          />
        }
      >
        <section aria-labelledby="workspace-heading">
          <h2 id="workspace-heading">Prepare the comparison</h2>
          <section aria-labelledby="target-heading">
            <h3 id="target-heading">Comparison target</h3>
          </section>
        </section>
      </AppShell>,
    );

    const levels = screen
      .getAllByRole("heading")
      .map((heading) => Number(heading.tagName.slice(1)));
    expect(levels[0]).toBe(1);
    for (let index = 1; index < levels.length; index += 1) {
      expect(levels[index]!).toBeLessThanOrEqual(levels[index - 1]! + 1);
    }
  });
});
