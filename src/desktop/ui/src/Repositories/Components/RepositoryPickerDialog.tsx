import { useEffect, useRef, useState } from "react";
import type { ActionError } from "../../Actions/Models/ActionError";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import { presentActionError } from "../../Actions/Services/presentActionError";
import { repositoryErrorTitles } from "../Constants/repositoryErrorTitles";
import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";
import { LocalIcon } from "../../Visuals/Components/LocalIcon";
import folderIcon from "../../assets/folder.svg";

type PickerState = "idle" | "choosing" | "opening" | "error";

interface RepositoryPickerDialogProps {
  readonly dismissible: boolean;
  readonly onDismiss: () => void;
  readonly onOpenRepository: (path: string) => Promise<RepositoryDescriptor>;
  readonly onRepositoryOpened?: (repository: RepositoryDescriptor) => void;
  readonly selectFolder?: () => Promise<string | null>;
  readonly selectedPath?: string;
  readonly state?: PickerState;
}

export function RepositoryPickerDialog({
  dismissible,
  onDismiss,
  onOpenRepository,
  onRepositoryOpened,
  selectFolder,
  selectedPath,
  state: suppliedState,
}: RepositoryPickerDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const chooseButtonRef = useRef<HTMLButtonElement>(null);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);
  const interactionGeneration = useRef(0);
  const [state, setState] = useState<PickerState>(suppliedState ?? "idle");
  const [path, setPath] = useState(selectedPath);
  const [error, setError] = useState<ActionError>();
  const effectiveState = suppliedState ?? state;

  function invalidateInteraction() {
    ++interactionGeneration.current;
  }

  useEffect(() => {
    const dialog = dialogRef.current;
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
  }, []);

  async function chooseFolder() {
    if (
      !selectFolder ||
      effectiveState === "choosing" ||
      effectiveState === "opening"
    ) {
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
        onRepositoryOpened?.(repository);
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
  const isBusy = effectiveState === "choosing" || effectiveState === "opening";

  return (
    <dialog
      className="repository-picker"
      ref={dialogRef}
      aria-describedby="repository-picker-description"
      aria-modal="true"
      aria-label="Open a repository"
      onCancel={(event) => {
        if (!dismissible) {
          event.preventDefault();
          return;
        }
        dismiss();
      }}
    >
      <h2>Open a repository</h2>
      <p id="repository-picker-description">
        Choose a Git working tree for ChangeLens to inspect locally and
        read-only.
      </p>
      {effectiveState === "opening" && path ? (
        <p role="status" aria-live="polite">
          Inspecting repository… <code>{path}</code>
        </p>
      ) : null}
      {presentation ? (
        <section role="alert">
          <strong>{presentation.title}</strong>
          <ul>
            {presentation.messages.map((message, index) => (
              <li key={`${error?.errors[index]?.code ?? "error"}-${index}`}>
                {message}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      <button
        ref={chooseButtonRef}
        type="button"
        disabled={isBusy}
        onClick={chooseFolder}
      >
        <LocalIcon source={folderIcon} />
        Choose folder
      </button>
      {dismissible ? (
        <button type="button" disabled={isBusy} onClick={dismiss}>
          Cancel
        </button>
      ) : null}
    </dialog>
  );
}
