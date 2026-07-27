using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the tagged remote-baseline union in the engine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(CurrentRemoteBaselineResult), "current")]
[JsonDerivedType(typeof(MovedRemoteBaselineResult), "moved")]
[JsonDerivedType(typeof(NoRemoteRemoteBaselineResult), "noRemote")]
internal abstract record RemoteBaselineResult;
