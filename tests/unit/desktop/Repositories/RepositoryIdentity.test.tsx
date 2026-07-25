import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RepositoryIdentity } from "../../../../src/desktop/ui/src/Repositories/Components/RepositoryIdentity";

describe("RepositoryIdentity", () => {
  it("shows the complete branch repository identity as text", () => {
    render(
      <RepositoryIdentity
        repository={{
          name: '<img onerror="alert(1)">',
          canonicalPath: "/very/long/path/to/change_lens",
          head: {
            kind: "branch",
            name: "feature/repository-intake",
            revision:
              "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
          },
        }}
      />,
    );

    expect(screen.getByText('<img onerror="alert(1)">')).toBeInTheDocument();
    expect(
      screen.getByText("/very/long/path/to/change_lens"),
    ).toBeInTheDocument();
    expect(screen.getByText("feature/repository-intake")).toBeInTheDocument();
    expect(
      screen.getByText(
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      ),
    ).toBeInTheDocument();
    expect(document.querySelector("img[onerror]")).not.toBeInTheDocument();
  });

  it("labels detached revisions truthfully", () => {
    render(
      <RepositoryIdentity
        repository={{
          name: "ChangeLens",
          canonicalPath: "/repo",
          head: { kind: "detached", revision: "abc123" },
        }}
      />,
    );

    expect(screen.getByText("Detached HEAD")).toBeInTheDocument();
    expect(screen.getByText("abc123")).toBeInTheDocument();
  });

  it("shows the complete SHA-1 revision without truncation", () => {
    const revision = "0123456789abcdef0123456789abcdef01234567";
    render(
      <RepositoryIdentity
        repository={{
          name: "ChangeLens",
          canonicalPath: "/repo",
          head: { kind: "branch", name: "main", revision },
        }}
      />,
    );

    expect(screen.getByText(revision)).toBeInTheDocument();
  });
});
