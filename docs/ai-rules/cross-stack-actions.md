# Adding a Cross-Stack Action

Read before adding or changing any action that crosses React, Tauri, Rust, or the Engine protocol. Read `architecture-boundaries.md` alongside this file.

Use **action** as the product-level term for work initiated by React across a backend boundary. Do not split actions into command and query hierarchies. Use **Tauri command** only when referring to Tauri's framework primitive, and retain established technical contract names such as `OperationError`.

Classify the action before adding transport code:

- UI-only actions remain in React.
- Native-only actions use one explicit typed Tauri command and do not enter the Engine protocol.
- Engine-backed actions use React → an explicit Tauri command → the shared Rust engine client/process →
  `EngineActionProcessor` → the action's `IActionHandler` → an already approved capability entry point.

For every engine-backed action:

1. Specify the capability behavior and its .NET entry point before designing transport types. Transport work does not authorize new Core or Infrastructure behavior.
2. Record the exact mapping from React client action, Tauri command, Rust client action, protocol action, and .NET
   processing target.
   Protocol actions use dotted camel case.
3. Add strict versioned request and result schemas under `contracts/engine-protocol`. Reject missing, unknown, duplicate, and incorrectly typed request properties. Add a `parameters` object only when the action has real input.
4. Keep protocol versions, request identifiers, and fixed action names out of React. Rust assigns them and exposes
   only the action's explicit typed Tauri command; never expose a generic `engine_action(action, parameters)` command.
5. Add one `IActionHandler` implementation for the action in its capability slice, and register it with
   `AddSingleton<IActionHandler, THandler>`. The handler's `Action` property returns its capability-owned
   action constant; the handler deserializes its own parameters and returns one correlated
   `ProtocolResponse`. `EngineActionProcessor` enumerates the registered handlers once into an ordinal,
   immutable-after-construction map and rejects blank or duplicate action names. Keep the processor free
   of action constants. Do not add a mediator library, reflection-based handler discovery, keyed DI, a
   service locator, a runtime-mutable registry, or generated protocol types.
6. Return exactly one correlated result or error. A typed action returns its typed payload; a payload-free action returns `result: null`. Fire-and-forget engine actions are not supported.
7. Preserve every expected error's `ErrorType`, stable code, safe message, order, and request identifier. An uncoded or unsafe error becomes a sanitized `InternalError` at the Engine boundary. Rust-originated failures use the approved `transport` or `protocol` kind; TypeScript normalizes all rejections to `ActionError`.
8. Never automatically replay a failed action. A later user- or React-initiated action may restart an invalidated engine process. Every future write specification must define a reconciliation action for uncertain transport outcomes.
9. Add contract, Engine protocol, Rust process, Tauri command, logging, and stdout-isolation tests in the same change. Verify the React client and presentation with formatting, linting, type checking, and production builds, then provide a focused manual UI/UX checklist for the user. Shared JSON fixtures must prove cross-language field names and shapes.

Keep the shared transport deliberately small. Extract only process, correlation, bounded-I/O, common response, and
error behavior that real actions share. Keep capability arguments, result validation, UI behavior, and stable action
selection in the capability slice.
