# Engine protocol contracts

This directory is the source of truth for implemented ChangeLens.Engine wire shapes.

Version 1 requests use an action envelope containing `protocolVersion`, `requestId`, and `action`. Rust owns the
protocol version, request identifier, and fixed action name; React does not create protocol messages directly.

Version 1 currently implements `engine.checkStatus` and `repositories.open`. The `engine.checkStatus` action takes no
input, so its request has no `parameters` property. The `repositories.open` action takes a repository path through its
`parameters` property. Add `parameters` to an action request only when that action has real input.

The `repositories.open` result describes a repository and its current head. Its `head` is a strict tagged union: a
branch head has `kind: "branch"`, a non-empty branch name, and a lowercase 40- or 64-character revision; a detached
head has `kind: "detached"` and the same revision shape without a branch name.

Every accepted request envelope receives exactly one correlated response. Successful actions return either a typed result or the
canonical payload-free `result: null`. Expected failures after envelope acceptance return the request identifier and a non-empty,
ordered `errors` collection so error identity and precedence remain stable across boundaries.

Input rejected before the strict common envelope is accepted returns an error with `requestId: null`. A client with one in-flight
exchange must surface that ordered Engine error without treating the missing identifier as a correlation mismatch. A different
non-null response identifier remains a correlation failure.

Each version directory contains strict action schemas, shared response schemas, and canonical fixtures reused by
.NET and Rust tests. When a wire shape changes, update its schema, fixtures, boundary models, and tests together.
