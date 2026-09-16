namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents the boost for shared keys between a changed file and a candidate in different recognized languages.
/// </summary>
/// <remarks>
///     The boost restates the shared-key signals it was computed from, so it is ordered after them and is never a
///     candidate's dominant family.
/// </remarks>
/// <param name="ChangedPath">The current repository-relative path of the changed file. Cannot be <see langword="null" />.</param>
/// <param name="Contribution">The positive amount this signal added to the candidate score.</param>
/// <param name="ChangedLanguage">The recognized language of the changed file. Cannot be <see langword="null" />.</param>
/// <param name="CandidateLanguage">The recognized language of the candidate file. Cannot be <see langword="null" />.</param>
public sealed record CrossLanguageCorrespondenceSignal(
    string ChangedPath,
    double Contribution,
    string ChangedLanguage,
    string CandidateLanguage) : CorrespondenceSignal(CorrespondenceSignalKind.CrossLanguage, ChangedPath, Contribution);
