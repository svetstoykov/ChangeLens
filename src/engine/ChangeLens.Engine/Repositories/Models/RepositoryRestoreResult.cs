using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents the tagged startup repository-restoration result.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(NoRepositoryRestoreResult), "none")]
[JsonDerivedType(typeof(RestoredRepositoryResult), "restored")]
internal abstract record RepositoryRestoreResult;
