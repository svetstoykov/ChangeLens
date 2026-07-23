using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents the current committed repository HEAD in the engine protocol.
/// </summary>
/// <param name="Revision">The full lowercase Git object identifier.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BranchRepositoryHeadResult), "branch")]
[JsonDerivedType(typeof(DetachedRepositoryHeadResult), "detached")]
internal abstract record RepositoryHeadResult(string Revision);
