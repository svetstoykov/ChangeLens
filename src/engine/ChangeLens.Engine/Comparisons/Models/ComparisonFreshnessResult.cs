using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the tagged comparison-freshness union in the engine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(CurrentComparisonFreshnessResult), "current")]
[JsonDerivedType(typeof(StaleComparisonFreshnessResult), "stale")]
internal abstract record ComparisonFreshnessResult;
