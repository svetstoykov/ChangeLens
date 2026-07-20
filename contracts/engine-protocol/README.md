# ChangeLens engine protocol

The desktop shell and `ChangeLens.Engine` communicate with newline-delimited JSON over standard input and standard output. Each line is one complete protocol message. Standard error is reserved for diagnostics.

Versioned schemas live under their corresponding version directory. The initial skeleton defines the `engine.getInfo` handshake used to prove the React → Tauri → .NET development path.
