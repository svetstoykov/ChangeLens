# Engine protocol contracts

This directory is the source of truth for implemented ChangeLens.Engine wire shapes.

Version 1 requests use an action envelope containing `protocolVersion`, `requestId`, and `action`. Rust owns the
protocol version, request identifier, and fixed action name; React does not create protocol messages directly.

Version 1 currently implements `engine.checkStatus`. The action takes no input, so its request has no `parameters`
property. Add `parameters` to an action request only when that action has real input.

Every accepted request envelope receives exactly one correlated response. Successful actions return either a typed result or the
canonical payload-free `result: null`. Expected failures after envelope acceptance return the request identifier and a non-empty,
ordered `errors` collection so error identity and precedence remain stable across boundaries.

Input rejected before the strict common envelope is accepted returns an error with `requestId: null`. A client with one in-flight
exchange must surface that ordered Engine error without treating the missing identifier as a correlation mismatch. A different
non-null response identifier remains a correlation failure.

Each version directory contains strict action schemas, shared response schemas, and canonical fixtures reused by
.NET and Rust tests. When a wire shape changes, update its schema, fixtures, boundary models, and tests together.
