using ChangeLens.Core.Analysis.ChangeAnatomy.Models;
using ChangeLens.Core.Analysis.Correspondence.Models;

namespace ChangeLens.Core.Analysis.Correspondence.Helpers;

/// <summary>
///     Validates configured bounds and weights for correspondence ranking.
/// </summary>
public static class CorrespondenceOptionsValidator
{
    /// <summary>
    ///     Validates correspondence ranking bounds, weights, and shares.
    /// </summary>
    /// <param name="options">The correspondence options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Any bound, weight, or share violates its invariant.</exception>
    public static void Validate(CorrespondenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumCandidates <= 0
            || options.MaximumIndexedKeysPerFile <= 0
            || options.MaximumReasonsPerCandidate <= 0
            || options.CommonKeyMinimumFileCount <= 0
            || !double.IsFinite(options.IdentifierWeight)
            || options.IdentifierWeight < 0
            || !double.IsFinite(options.IdentifierPartWeight)
            || options.IdentifierPartWeight < 0
            || !double.IsFinite(options.StringLiteralWeight)
            || options.StringLiteralWeight < 0
            || !double.IsFinite(options.LiteralSegmentWeight)
            || options.LiteralSegmentWeight < 0
            || !double.IsFinite(options.PathStemWeight)
            || options.PathStemWeight < 0
            || !double.IsFinite(options.CommentWordWeight)
            || options.CommentWordWeight < 0
            || !double.IsFinite(options.CrossLanguageBoost)
            || options.CrossLanguageBoost < 0
            || !double.IsFinite(options.CoChangeWeight)
            || options.CoChangeWeight < 0
            || !double.IsFinite(options.MaximumCoChangeContributionPerPair)
            || options.MaximumCoChangeContributionPerPair <= 0
            || !double.IsFinite(options.MaximumCoChangeContributionPerCandidate)
            || options.MaximumCoChangeContributionPerCandidate <= 0
            || !double.IsFinite(options.CoChangeHalfLifeInCommits)
            || options.CoChangeHalfLifeInCommits <= 0
            || !double.IsFinite(options.CommonKeyFileRatio)
            || options.CommonKeyFileRatio <= 0
            || options.CommonKeyFileRatio > 1
            || !double.IsFinite(options.MaximumDominantSignalShare)
            || options.MaximumDominantSignalShare <= 0
            || options.MaximumDominantSignalShare > 1)
        {
            throw new InvalidOperationException("Correspondence options must contain valid bounds and weights.");
        }
    }

    /// <summary>
    ///     Validates correspondence ranking options together with the shared change-anatomy tokenization bounds.
    /// </summary>
    /// <param name="options">The correspondence options to validate. Cannot be <see langword="null" />.</param>
    /// <param name="anatomyOptions">The shared change-anatomy options. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="options" /> or <paramref name="anatomyOptions" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">Any bound, weight, or share violates its invariant.</exception>
    public static void Validate(CorrespondenceOptions options, ChangeAnatomyOptions anatomyOptions)
    {
        ArgumentNullException.ThrowIfNull(anatomyOptions);
        Validate(options);

        if (anatomyOptions.MinimumKeyLength <= 0 || anatomyOptions.MaximumOccurrencesPerKey <= 0)
        {
            throw new InvalidOperationException("Correspondence options must contain valid bounds and weights.");
        }
    }
}
