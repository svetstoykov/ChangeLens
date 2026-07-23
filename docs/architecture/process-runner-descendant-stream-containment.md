# Descendant stream containment in the Git runner

## Scenario

The Git runner redirects standard output and standard error so it can bound and classify tool output. A launched process can start a descendant that inherits either redirected stream, then exit without waiting for that descendant. The descendant keeps the pipe open, so the runner does not observe end-of-file even though the tracked parent process has exited.

For the current repository-inspection commands, this is unlikely with a normal installed Git executable: the fixed `rev-parse` and `symbolic-ref` operations do not normally start long-lived helpers, paging is disabled, prompts are disabled, and the runner starts Git directly. It can still occur when the resolved `git` executable is a wrapper, is compromised or misconfigured, or if a future approved tool operation starts a helper process. The controlled integration fixture reproduces the case by starting a child that inherits both redirected streams and immediately exiting its parent.

## Observed behavior

`GitCliCommandRunner` currently waits for the parent process and then for both redirected streams. Once the parent has exited, `Process.Kill(entireProcessTree: true)` can no longer terminate its descendants. If a descendant holds a stream open, stream reads do not complete; the runner can therefore outlive its internal deadline and ignore caller cancellation.

The retained regression tests demonstrate both outcomes under a five-second outer test bound:

- an internal one-second deadline does not return the expected `git.timedOut` Result;
- a one-second caller cancellation does not throw the expected `OperationCanceledException`.

The tests deliberately do not perform test-owned child cleanup. They must remain failing until the production runner owns containment and reaping.

## Deferred decision

Cancelling the stream readers is not sufficient: it releases the caller but leaves an unowned descendant process. Correct containment must be established before Git can create descendants.

There is no .NET 10 cross-platform `ProcessStartInfo` facility that creates a kill-on-close process container. A correct direct-launch solution needs platform-native containment:

- POSIX: create a private process group at spawn time and terminate that group on timeout or cancellation.
- Windows: create the process suspended, assign it to a Job Object, then resume it, or use an equivalent pre-execution job-list mechanism.

Do not introduce a shell or supervisor wrapper: the repository inspection boundary must continue to launch Git directly. This work is deferred pending a dedicated cross-platform process-containment design. Until then, the regression remains intentionally failing and Task 5 cannot be accepted.
