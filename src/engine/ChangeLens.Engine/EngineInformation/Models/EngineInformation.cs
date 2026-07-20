namespace ChangeLens.Engine.EngineInformation.Models;

/// <summary>
///     Represents identifying information reported by the running engine.
/// </summary>
/// <param name="Name">The stable name of the engine executable.</param>
/// <param name="Version">The semantic version of the running engine.</param>
/// <param name="ProtocolVersion">The protocol version supported by the running engine.</param>
public sealed record EngineInformation(
    string Name,
    string Version,
    int ProtocolVersion);
