namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents configurable bounds and weights for correspondence ranking.
/// </summary>
/// <remarks>
///     The defaults are the values the analysis prototype measured. Weights may be zero to silence a key kind; every
///     other numeric value must be positive, and the ratio and share values must not exceed one.
/// </remarks>
public sealed class CorrespondenceOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of candidates returned. The default is 40 candidates.
    /// </summary>
    public int MaximumCandidates { get; set; } = 40;

    /// <summary>
    ///     Gets or sets the maximum distinct keys indexed per tree file. The default is 2,000 keys.
    /// </summary>
    public int MaximumIndexedKeysPerFile { get; set; } = 2_000;

    /// <summary>
    ///     Gets or sets the maximum signals kept as display reasons per candidate. The default is 16 reasons.
    /// </summary>
    public int MaximumReasonsPerCandidate { get; set; } = 16;

    /// <summary>
    ///     Gets or sets a value indicating whether first-parent co-change history contributes signals.
    /// </summary>
    /// <value>
    ///     <see langword="true" /> if history is read and scored; otherwise, <see langword="false" />. The default is
    ///     <see langword="true" />.
    /// </value>
    public bool IncludeCoChange { get; set; } = true;

    /// <summary>
    ///     Gets or sets the weight of a shared identifier. The default is 1.0.
    /// </summary>
    public double IdentifierWeight { get; set; } = 1.0;

    /// <summary>
    ///     Gets or sets the weight of a shared identifier part. The default is 0.75.
    /// </summary>
    public double IdentifierPartWeight { get; set; } = 0.75;

    /// <summary>
    ///     Gets or sets the weight of a shared complete string literal. The default is 1.5.
    /// </summary>
    public double StringLiteralWeight { get; set; } = 1.5;

    /// <summary>
    ///     Gets or sets the weight of a shared literal segment. The default is 1.25.
    /// </summary>
    public double LiteralSegmentWeight { get; set; } = 1.25;

    /// <summary>
    ///     Gets or sets the weight of a shared path stem. The default is 0.8.
    /// </summary>
    public double PathStemWeight { get; set; } = 0.8;

    /// <summary>
    ///     Gets or sets the weight of a shared comment word, the weakest key kind. The default is 0.35.
    /// </summary>
    public double CommentWordWeight { get; set; } = 0.35;

    /// <summary>
    ///     Gets or sets the fraction of shared-key contribution added when two files cross a language boundary. The
    ///     default is 0.15.
    /// </summary>
    public double CrossLanguageBoost { get; set; } = 0.15;

    /// <summary>
    ///     Gets or sets the contribution of one recency-weighted co-change commit. The default is 0.35.
    /// </summary>
    public double CoChangeWeight { get; set; } = 0.35;

    /// <summary>
    ///     Gets or sets the co-change contribution ceiling for one changed file and one candidate. The default is 3.0.
    /// </summary>
    public double MaximumCoChangeContributionPerPair { get; set; } = 3.0;

    /// <summary>
    ///     Gets or sets the total co-change contribution ceiling for one candidate. The default is 6.0.
    /// </summary>
    public double MaximumCoChangeContributionPerCandidate { get; set; } = 6.0;

    /// <summary>
    ///     Gets or sets the number of history commits after which a co-change counts half. The default is 50 commits.
    /// </summary>
    public double CoChangeHalfLifeInCommits { get; set; } = 50.0;

    /// <summary>
    ///     Gets or sets the share of indexed files above which a key is too common to query. The default is 0.2.
    /// </summary>
    public double CommonKeyFileRatio { get; set; } = 0.2;

    /// <summary>
    ///     Gets or sets the smallest file count at which a key can be too common to query. The default is 10 files.
    /// </summary>
    public int CommonKeyMinimumFileCount { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the largest share of returned candidates one dominant signal family may fill while other
    ///     candidates remain. The default is 0.6.
    /// </summary>
    public double MaximumDominantSignalShare { get; set; } = 0.6;
}
