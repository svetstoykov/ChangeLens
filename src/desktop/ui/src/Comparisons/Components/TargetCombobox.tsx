import { useEffect, useId, useRef, useState } from "react";
import type { ComparisonTarget } from "../Models/ComparisonTarget";
import { Icon } from "../../Visuals/Components/Icon";

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
  const listboxRef = useRef<HTMLDivElement>(null);
  const [expanded, setExpanded] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const localTargets = targets.filter((target) => target.kind === "local");
  const remoteTargets = targets.filter(
    (target) => target.kind === "remoteTracking",
  );
  const groupedTargets = [...localTargets, ...remoteTargets];
  const clampedActiveIndex =
    groupedTargets.length === 0
      ? -1
      : Math.min(activeIndex, groupedTargets.length - 1);

  useEffect(() => {
    if (!expanded || clampedActiveIndex < 0) return;
    listboxRef.current
      ?.querySelector<HTMLElement>('[data-active="true"]')
      ?.scrollIntoView?.({ block: "nearest" });
  }, [expanded, clampedActiveIndex]);

  function moveActive(next: number) {
    if (groupedTargets.length === 0) return;
    setExpanded(true);
    setActiveIndex(Math.max(0, Math.min(next, groupedTargets.length - 1)));
  }

  function chooseActive() {
    const target = groupedTargets[clampedActiveIndex];
    if (target === undefined) return;
    onSelect(target);
    setActiveIndex(-1);
    setExpanded(false);
  }

  return (
    <section
      className="target-combobox"
      aria-labelledby="comparison-target-heading"
      onBlur={(event) => {
        if (
          !(event.relatedTarget instanceof Node) ||
          !event.currentTarget.contains(event.relatedTarget)
        ) {
          setExpanded(false);
          setActiveIndex(-1);
        }
      }}
    >
      <header className="setup-card-heading">
        <span className="step-number" aria-hidden="true">
          1
        </span>
        <div>
          <p className="eyebrow">Comparison target</p>
          <h3 id="comparison-target-heading">Choose your baseline</h3>
          <p className="section-description">
            Compare the current work against a local or cached remote branch.
            Contacting the server only happens for the branch you select as a
            baseline, and only to read a single ref.
          </p>
        </div>
      </header>
      <label className="field-label" htmlFor={`${listId}-input`}>
        Search branches
      </label>
      <div className="combobox">
        <div className="combobox-field" data-expanded={expanded}>
          <Icon name="branch" />
          <input
            id={`${listId}-input`}
            role="combobox"
            aria-autocomplete="list"
            aria-controls={listId}
            aria-expanded={expanded}
            aria-activedescendant={
              expanded && clampedActiveIndex >= 0
                ? `${listId}-option-${clampedActiveIndex}`
                : undefined
            }
            autoComplete="off"
            placeholder="Search local and cached remote branches"
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
                  moveActive(clampedActiveIndex + 1);
                  break;
                case "ArrowUp":
                  event.preventDefault();
                  moveActive(clampedActiveIndex - 1);
                  break;
                case "Home":
                  event.preventDefault();
                  moveActive(0);
                  break;
                case "End":
                  event.preventDefault();
                  moveActive(groupedTargets.length - 1);
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
          <Icon name="chevronDown" className="combobox-chevron" />
        </div>
        {expanded ? (
          <div className="target-listbox" ref={listboxRef}>
            <div
              className="target-listbox-options"
              id={listId}
              role="listbox"
              aria-label="Comparison targets"
            >
              <TargetGroup
                id={listId}
                label="Local branches"
                targets={localTargets}
                startIndex={0}
                activeIndex={clampedActiveIndex}
                selectedTarget={selectedTarget}
                onSelect={(target) => {
                  onSelect(target);
                  setActiveIndex(-1);
                  setExpanded(false);
                }}
              />
              <TargetGroup
                id={listId}
                label="Cached remote branches"
                targets={remoteTargets}
                startIndex={localTargets.length}
                activeIndex={clampedActiveIndex}
                selectedTarget={selectedTarget}
                onSelect={(target) => {
                  onSelect(target);
                  setActiveIndex(-1);
                  setExpanded(false);
                }}
              />
            </div>
            {targets.length === 0 && !isDiscovering ? (
              <div className="target-empty">
                <Icon name="info" />
                <p>No supported targets match this search.</p>
              </div>
            ) : null}
            {nextCursor !== null ? (
              <button
                className="load-more-button"
                type="button"
                onClick={onLoadMore}
                disabled={isDiscovering}
              >
                Load more targets
              </button>
            ) : null}
          </div>
        ) : null}
      </div>
      <div
        className="selected-target"
        data-selected={selectedTarget !== null}
        aria-live="polite"
      >
        <span className="selected-target-icon">
          <Icon name={selectedTarget ? "check" : "compare"} />
        </span>
        <span>
          <small>{selectedTarget ? "Selected baseline" : "Next step"}</small>
          {selectedTarget ? (
            <code>{selectedTarget.name}</code>
          ) : (
            <strong>Choose a branch to prepare the comparison</strong>
          )}
        </span>
      </div>
      <footer className="target-meta">
        {unsupportedTargetCount > 0 ? (
          <p>
            <Icon name="info" />
            {unsupportedTargetCount} unsupported target
            {unsupportedTargetCount === 1 ? " is" : "s are"} hidden.
          </p>
        ) : (
          <span />
        )}
        {isDiscovering ? (
          <p className="inline-progress" role="status">
            <Icon name="refresh" />
            Loading targets…
          </p>
        ) : null}
      </footer>
    </section>
  );
}

interface TargetGroupProps {
  readonly id: string;
  readonly label: string;
  readonly targets: readonly ComparisonTarget[];
  readonly startIndex: number;
  readonly activeIndex: number;
  readonly selectedTarget: ComparisonTarget | null;
  readonly onSelect: (target: ComparisonTarget) => void;
}

function TargetGroup({
  id,
  label,
  targets,
  startIndex,
  activeIndex,
  selectedTarget,
  onSelect,
}: TargetGroupProps) {
  if (targets.length === 0) return null;
  return (
    <div className="target-group" role="group" aria-label={label}>
      <strong>{label}</strong>
      {targets.map((target, index) => {
        const targetIndex = startIndex + index;
        return (
          <button
            id={`${id}-option-${targetIndex}`}
            key={target.fullName}
            role="option"
            type="button"
            tabIndex={-1}
            aria-selected={target.fullName === selectedTarget?.fullName}
            data-active={targetIndex === activeIndex ? "true" : undefined}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => onSelect(target)}
          >
            <Icon name="branch" />
            <code>{target.name}</code>
            {target.fullName === selectedTarget?.fullName ? (
              <Icon name="check" className="option-check" />
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
