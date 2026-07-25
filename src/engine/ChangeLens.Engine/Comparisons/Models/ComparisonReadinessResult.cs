using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the tagged comparison-readiness union in the engine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(ReadyComparisonReadinessResult), "ready")]
[JsonDerivedType(typeof(EmptyComparisonReadinessResult), "empty")]
[JsonDerivedType(typeof(ConflictsComparisonReadinessResult), "conflicts")]
internal abstract record ComparisonReadinessResult;
