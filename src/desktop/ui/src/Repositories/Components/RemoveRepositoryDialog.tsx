import { useEffect, useRef, type RefObject } from "react";
import type { RecentRepository } from "../Models/RecentRepository";
import { Icon } from "../../Visuals/Components/Icon";

interface RemoveRepositoryDialogProps {
  readonly repository: RecentRepository;
  readonly busy: boolean;
  readonly fallbackFocusRef: RefObject<HTMLElement | null>;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
}

export function RemoveRepositoryDialog({
  repository,
  busy,
  fallbackFocusRef,
  onConfirm,
  onCancel,
}: RemoveRepositoryDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const cancelButtonRef = useRef<HTMLButtonElement>(null);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    previouslyFocusedElementRef.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    if (dialog && !dialog.open) {
      dialog.showModal();
    }
    cancelButtonRef.current?.focus();
    return () => {
      if (dialog?.open) {
        dialog.close();
      }
      const previous = previouslyFocusedElementRef.current;
      if (
        previous?.isConnected &&
        !(previous instanceof HTMLButtonElement && previous.disabled)
      ) {
        previous.focus();
      } else {
        fallbackFocusRef.current?.focus();
      }
    };
  }, [fallbackFocusRef]);

  return (
    <dialog
      className="repository-picker remove-repository-dialog"
      ref={dialogRef}
      aria-labelledby="remove-repository-heading"
      aria-describedby="remove-repository-description"
      aria-modal="true"
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) {
          onCancel();
        }
      }}
    >
      <div className="repository-picker-hero">
        <span className="picker-illustration remove-repository-illustration">
          <Icon name="warning" />
        </span>
        <div>
          <p className="eyebrow">Local repository history</p>
          <h2 id="remove-repository-heading">
            Remove {repository.name} from history?
          </h2>
          <p id="remove-repository-description">
            ChangeLens will remove its metadata for this worktree from history.
            Repository files will not be changed.
          </p>
        </div>
      </div>
      <div className="remove-repository-target">
        <code title={repository.canonicalPath}>{repository.canonicalPath}</code>
      </div>
      <footer className="repository-picker-actions">
        <button
          ref={cancelButtonRef}
          className="secondary-button"
          type="button"
          disabled={busy}
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          className="primary-button"
          type="button"
          disabled={busy}
          onClick={onConfirm}
        >
          <Icon name={busy ? "refresh" : "warning"} />
          {busy ? "Removing…" : "Remove from history"}
        </button>
      </footer>
    </dialog>
  );
}
