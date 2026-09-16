using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents a rare normalized key found in both a changed file and a candidate file.
/// </summary>
/// <remarks>
///     The family in <see cref="CorrespondenceSignal.Kind" /> follows the weaker of the two key kinds, so a literal on
///     one side that matches an identifier on the other counts as identifier evidence.
/// </remarks>
/// <param name="Kind">
///     The signal family: <see cref="CorrespondenceSignalKind.SharedIdentifier" />,
///     <see cref="CorrespondenceSignalKind.SharedLiteral" />, <see cref="CorrespondenceSignalKind.SharedComment" />, or
///     <see cref="CorrespondenceSignalKind.PathAffinity" />.
/// </param>
/// <param name="ChangedPath">The current repository-relative path of the changed file. Cannot be <see langword="null" />.</param>
/// <param name="Contribution">The positive amount this signal added to the candidate score.</param>
/// <param name="MatchedValue">The shared normalized key. Cannot be <see langword="null" />.</param>
/// <param name="ChangedKeyKind">The strongest kind the key carries in the changed file.</param>
/// <param name="CandidateKeyKind">The strongest kind the key carries in the candidate file.</param>
/// <param name="DocumentFrequency">The number of indexed files that contain the key.</param>
/// <param name="CandidateLines">
///     The ascending one-based candidate lines holding the key, empty for a path-only match. Cannot be
///     <see langword="null" />.
/// </param>
public sealed record SharedKeyCorrespondenceSignal(
    CorrespondenceSignalKind Kind,
    string ChangedPath,
    double Contribution,
    string MatchedValue,
    ChangeAnatomyKeyKind ChangedKeyKind,
    ChangeAnatomyKeyKind CandidateKeyKind,
    int DocumentFrequency,
    IReadOnlyList<int> CandidateLines) : CorrespondenceSignal(Kind, ChangedPath, Contribution);
