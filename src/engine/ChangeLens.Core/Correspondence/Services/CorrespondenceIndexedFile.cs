namespace ChangeLens.Core.Correspondence.Services;

/// <summary>
///     Represents one captured tree file indexed for correspondence matching.
/// </summary>
/// <param name="Id">The zero-based position of the file in the indexed list.</param>
/// <param name="Path">The repository-relative path in the captured HEAD tree. Cannot be <see langword="null" />.</param>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Language">The recognized language, or <see langword="null" /> when the path is not recognized.</param>
/// <param name="Tokens">
///     The indexed tokens by normalized key. Cannot be <see langword="null" />.
/// </param>
internal sealed record CorrespondenceIndexedFile(
    int Id,
    string Path,
    string ObjectId,
    string? Language,
    IReadOnlyDictionary<string, CorrespondenceIndexedToken> Tokens);
