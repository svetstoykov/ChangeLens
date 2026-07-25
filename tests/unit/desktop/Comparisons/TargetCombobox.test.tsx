import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TargetCombobox } from "../../../../src/desktop/ui/src/Comparisons/Components/TargetCombobox";
import type { ComparisonTarget } from "../../../../src/desktop/ui/src/Comparisons/Models/ComparisonTarget";

afterEach(cleanup);

const localTarget: ComparisonTarget = {
  kind: "local",
  name: "main",
  fullName: "refs/heads/main",
  revision: "a".repeat(40),
};
const remoteTarget: ComparisonTarget = {
  kind: "remoteTracking",
  name: "origin/release",
  fullName: "refs/remotes/origin/release",
  revision: "b".repeat(40),
};

describe("TargetCombobox", () => {
  it("keeps keyboard activity separate from the committed selection", async () => {
    const user = userEvent.setup();
    render(<ControlledCombobox />);
    const input = screen.getByRole("combobox", { name: "Find a target" });

    await user.click(input);
    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "ArrowDown" });

    const localOption = screen.getByRole("option", { name: "main" });
    const remoteOption = screen.getByRole("option", {
      name: "origin/release",
    });
    expect(input).toHaveAttribute("aria-activedescendant", remoteOption.id);
    expect(localOption).toHaveAttribute("aria-selected", "true");
    expect(remoteOption).toHaveAttribute("aria-selected", "false");
    expect(remoteOption).toHaveAttribute("data-active", "true");

    fireEvent.keyDown(input, { key: "Enter" });

    expect(input).toHaveAttribute("aria-expanded", "false");
    expect(input).not.toHaveAttribute("aria-activedescendant");
    expect(screen.getByText("Selected:")).toHaveTextContent("origin/release");

    fireEvent.keyDown(input, { key: "ArrowDown" });

    expect(input).toHaveAttribute(
      "aria-activedescendant",
      screen.getByRole("option", { name: "main" }).id,
    );
    expect(screen.getByRole("option", { name: "main" })).toHaveAttribute(
      "aria-selected",
      "false",
    );
    expect(
      screen.getByRole("option", { name: "origin/release" }),
    ).toHaveAttribute("aria-selected", "true");
  });

  it("clears keyboard activity after pointer selection closes the list", async () => {
    const user = userEvent.setup();
    render(<ControlledCombobox />);
    const input = screen.getByRole("combobox", { name: "Find a target" });

    await user.click(input);
    fireEvent.keyDown(input, { key: "End" });
    await user.click(screen.getByRole("option", { name: "origin/release" }));

    expect(input).toHaveAttribute("aria-expanded", "false");
    expect(input).not.toHaveAttribute("aria-activedescendant");

    fireEvent.keyDown(input, { key: "ArrowDown" });

    expect(input).toHaveAttribute(
      "aria-activedescendant",
      screen.getByRole("option", { name: "main" }).id,
    );
    expect(
      screen.getByRole("option", { name: "origin/release" }),
    ).toHaveAttribute("aria-selected", "true");
  });

  it("clears keyboard activity when Escape closes the list", () => {
    render(<ControlledCombobox />);
    const input = screen.getByRole("combobox", { name: "Find a target" });

    input.focus();
    fireEvent.keyDown(input, { key: "Home" });
    expect(input).toHaveAttribute("aria-activedescendant");

    fireEvent.keyDown(input, { key: "Escape" });

    expect(input).toHaveAttribute("aria-expanded", "false");
    expect(input).not.toHaveAttribute("aria-activedescendant");
    expect(input).toHaveFocus();
  });
});

function ControlledCombobox() {
  const [selectedTarget, setSelectedTarget] =
    useState<ComparisonTarget>(localTarget);
  const [query, setQuery] = useState("");

  return (
    <TargetCombobox
      targets={[localTarget, remoteTarget]}
      selectedTarget={selectedTarget}
      query={query}
      nextCursor={null}
      unsupportedTargetCount={0}
      isDiscovering={false}
      onQueryChange={setQuery}
      onSelect={setSelectedTarget}
      onLoadMore={vi.fn()}
    />
  );
}
