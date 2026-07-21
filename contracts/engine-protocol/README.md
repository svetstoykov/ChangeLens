# Engine protocol contracts

This directory is the source of truth for implemented ChangeLens.Engine wire shapes.

Each version directory contains:

- shared response and error schemas used by more than one action shape;
- one strict request/result schema per implemented engine-backed action;
- canonical fixtures reused by .NET and Rust tests.

Version 1 currently implements `engine.getInfo`. React never creates these messages directly: Rust assigns the protocol version, request identifier, and fixed method. Every request receives exactly one correlated typed result, payload-free `result: null`, or non-empty ordered error response.

When a wire shape changes, update its schema, fixtures, .NET models, Rust models, TypeScript normalization, and boundary tests together. Do not add placeholder parameters or speculative action schemas.
