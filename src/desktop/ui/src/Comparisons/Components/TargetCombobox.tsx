import { useId, useState } from "react";
import type { ComparisonTarget } from "../Models/ComparisonTarget";

interface TargetComboboxProps {
  readonly targets: readonly ComparisonTarget[];
  readonly selectedTarget: ComparisonTarget | null;
  readonly query: string;
  readonly nextCursor: string | null;
  readonly unsupportedTargetCount: number;
  readonly isDiscovering: boolean;
  readonly onQueryChange: (query: string) => void;
  readonly onSelect: (target: ComparisonTarget) => void;
  readonly onLoadMore: () => void;
}

export function TargetCombobox({
  targets,
  selectedTarget,
  query,
  nextCursor,
  unsupportedTargetCount,
  isDiscovering,
  onQueryChange,
  onSelect,
  onLoadMore,
}: TargetComboboxProps) {
  const listId = useId();
  const [expanded, setExpanded] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const localTargets = targets.filter((target) => target.kind === "local");
  const remoteTargets = targets.filter(
    (target) => target.kind === "remoteTracking",
  );

  function moveActive(next: number) {
    if (targets.length === 0) return;
    setExpanded(true);
    setActiveIndex(Math.max(0, Math.min(next, targets.length - 1)));
  }

  function chooseActive() {
    const target = targets[activeIndex];
    if (target === undefined) return;
    onSelect(target);
    setExpanded(false);
  }

  return (
    <section aria-labelledby="comparison-target-heading">
      <h2 id="comparison-target-heading">Comparison target</h2>
      <p>
        Targets are local branches or cached remote-tracking references.
        ChangeLens does not use the network.
      </p>
      <label htmlFor={`${listId}-input`}>Find a target</label>
      <input
        id={`${listId}-input`}
        role="combobox"
        aria-autocomplete="list"
        aria-controls={listId}
        aria-expanded={expanded}
        aria-activedescendant={
          activeIndex >= 0 ? `${listId}-option-${activeIndex}` : undefined
        }
        value={query}
        onChange={(event) => {
          onQueryChange(event.target.value);
          setExpanded(true);
          setActiveIndex(-1);
        }}
        onFocus={() => setExpanded(true)}
        onKeyDown={(event) => {
          switch (event.key) {
            case "ArrowDown":
              event.preventDefault();
              moveActive(activeIndex + 1);
              break;
            case "ArrowUp":
              event.preventDefault();
              moveActive(activeIndex - 1);
              break;
            case "Home":
              event.preventDefault();
              moveActive(0);
              break;
            case "End":
              event.preventDefault();
              moveActive(targets.length - 1);
              break;
            case "Enter":
              event.preventDefault();
              chooseActive();
              break;
            case "Escape":
              setExpanded(false);
              setActiveIndex(-1);
              break;
          }
        }}
      />
      {selectedTarget ? (
        <p aria-live="polite">Selected: {selectedTarget.name}</p>
      ) : (
        <p aria-live="polite">Select a comparison target to continue.</p>
      )}
      {expanded ? (
        <div id={listId} role="listbox" aria-label="Comparison targets">
          <TargetGroup
            id={listId}
            label="Local branches"
            targets={localTargets}
            startIndex={0}
            activeIndex={activeIndex}
            onSelect={(target) => {
              onSelect(target);
              setExpanded(false);
            }}
          />
          <TargetGroup
            id={listId}
            label="Cached remote branches"
            targets={remoteTargets}
            startIndex={localTargets.length}
            activeIndex={activeIndex}
            onSelect={(target) => {
              onSelect(target);
              setExpanded(false);
            }}
          />
          {targets.length === 0 && !isDiscovering ? (
            <p>No supported comparison targets match this search.</p>
          ) : null}
          {nextCursor !== null ? (
            <button type="button" onClick={onLoadMore} disabled={isDiscovering}>
              Load more targets
            </button>
          ) : null}
        </div>
      ) : null}
      {unsupportedTargetCount > 0 ? (
        <p>
          {unsupportedTargetCount} unsupported target
          {unsupportedTargetCount === 1 ? " is" : "s are"} not shown.
        </p>
      ) : null}
      {isDiscovering ? <p role="status">Loading comparison targets…</p> : null}
    </section>
  );
}

interface TargetGroupProps {
  readonly id: string;
  readonly label: string;
  readonly targets: readonly ComparisonTarget[];
  readonly startIndex: number;
  readonly activeIndex: number;
  readonly onSelect: (target: ComparisonTarget) => void;
}

function TargetGroup({
  id,
  label,
  targets,
  startIndex,
  activeIndex,
  onSelect,
}: TargetGroupProps) {
  if (targets.length === 0) return null;
  return (
    <div role="group" aria-label={label}>
      <strong>{label}</strong>
      {targets.map((target, index) => {
        const targetIndex = startIndex + index;
        return (
          <button
            id={`${id}-option-${targetIndex}`}
            key={target.fullName}
            role="option"
            type="button"
            aria-selected={targetIndex === activeIndex}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => onSelect(target)}
          >
            {target.name}
          </button>
        );
      })}
    </div>
  );
}
