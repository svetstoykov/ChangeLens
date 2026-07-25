import { useEffect, useRef, useState } from "react";
import type { ActionError } from "../../Actions/Models/ActionError";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { repositoryErrorTitles } from "../Constants/repositoryErrorTitles";
import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";
import { Icon } from "../../Visuals/Components/Icon";

type PickerState = "idle" | "choosing" | "opening" | "error";

interface RepositoryPickerDialogProps {
  readonly dismissible: boolean;
  readonly onDismiss: () => void;
  readonly onOpenRepository: (path: string) => Promise<RepositoryDescriptor>;
  readonly onRepositoryOpened: (repository: RepositoryDescriptor) => void;
  readonly selectFolder: () => Promise<string | null>;
}

export function RepositoryPickerDialog({
  dismissible,
  onDismiss,
  onOpenRepository,
  onRepositoryOpened,
  selectFolder,
}: RepositoryPickerDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const chooseButtonRef = useRef<HTMLButtonElement>(null);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);
  const interactionGeneration = useRef(0);
  const [state, setState] = useState<PickerState>("idle");
  const [path, setPath] = useState<string>();
  const [error, setError] = useState<ActionError>();

  function invalidateInteraction() {
    ++interactionGeneration.current;
  }

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dismissible) {
      chooseButtonRef.current?.focus();
      return () => invalidateInteraction();
    }

    previouslyFocusedElementRef.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    dialog?.showModal();
    chooseButtonRef.current?.focus();
    return () => {
      invalidateInteraction();
      dialog?.close();
      if (previouslyFocusedElementRef.current?.isConnected) {
        previouslyFocusedElementRef.current.focus();
      }
    };
  }, [dismissible]);

  async function chooseFolder() {
    if (state === "choosing" || state === "opening") {
      return;
    }

    const generation = ++interactionGeneration.current;
    setError(undefined);
    setState("choosing");
    try {
      const chosenPath = await selectFolder();
      if (generation !== interactionGeneration.current) {
        return;
      }
      if (chosenPath === null) {
        setState("idle");
        return;
      }

      setPath(chosenPath);
      setState("opening");
      const repository = await onOpenRepository(chosenPath);
      if (generation === interactionGeneration.current) {
        onRepositoryOpened(repository);
      }
    } catch (reason: unknown) {
      if (generation === interactionGeneration.current) {
        setError(normalizeActionError(reason));
        setState("error");
      }
    }
  }

  function dismiss() {
    invalidateInteraction();
    if (dismissible) {
      onDismiss();
    }
  }

  const presentation = error
    ? presentActionError(error, repositoryErrorTitles)
    : undefined;
  const isBusy = state === "choosing" || state === "opening";

  const content = (
    <>
      <div className="repository-picker-hero">
        <span className="picker-illustration">
          <Icon name="folder" />
        </span>
        <div>
          <p className="eyebrow">
            {dismissible ? "Switch workspace" : "Welcome to ChangeLens"}
          </p>
          <h2 id="repository-picker-heading">
            {dismissible ? "Open another repository" : "Open a Git repository"}
          </h2>
          <p id="repository-picker-description">
            Choose a local working tree. ChangeLens inspects it read-only and
            never contacts a remote.
          </p>
        </div>
      </div>
      <div className="picker-assurances" aria-label="Repository access">
        <span>
          <Icon name="shield" />
          Local inspection
        </span>
        <span>
          <Icon name="check" />
          Read-only access
        </span>
      </div>
      {state === "opening" && path ? (
        <div className="picker-progress" role="status" aria-live="polite">
          <Icon name="refresh" />
          <span>
            <strong>Inspecting repository</strong>
            <code>{path}</code>
          </span>
        </div>
      ) : null}
      {presentation ? (
        <section className="picker-error" role="alert">
          <Icon name="warning" />
          <div>
            <strong>{presentation.title}</strong>
            <ul>
              {presentation.messages.map((message, index) => (
                <li key={`${error?.errors[index]?.code ?? "error"}-${index}`}>
                  {message}
                </li>
              ))}
            </ul>
          </div>
        </section>
      ) : null}
      <footer className="repository-picker-actions">
        {dismissible ? (
          <button
            className="secondary-button"
            type="button"
            disabled={isBusy}
            onClick={dismiss}
          >
            Cancel
          </button>
        ) : null}
        <button
          ref={chooseButtonRef}
          className="primary-button"
          type="button"
          disabled={isBusy}
          onClick={chooseFolder}
        >
          <Icon name={isBusy ? "refresh" : "folder"} />
          {state === "choosing"
            ? "Opening folder picker…"
            : state === "opening"
              ? "Inspecting repository…"
              : "Choose repository folder"}
        </button>
      </footer>
    </>
  );

  if (!dismissible) {
    return (
      <section
        className="repository-picker repository-picker-inline"
        aria-labelledby="repository-picker-heading"
        aria-describedby="repository-picker-description"
      >
        {content}
      </section>
    );
  }

  return (
    <dialog
      className="repository-picker"
      ref={dialogRef}
      aria-labelledby="repository-picker-heading"
      aria-describedby="repository-picker-description"
      aria-modal="true"
      data-dismissible
      onCancel={dismiss}
    >
      {content}
    </dialog>
  );
}
