namespace ChangeLens.Core.Comparisons.Constants;

/// <summary>
///     Provides bounded limits for comparison operations and protocol results.
/// </summary>
internal static class ComparisonLimits
{
    /// <summary>
    ///     Gets the total budget for comparison-target discovery.
    /// </summary>
    internal static readonly TimeSpan TargetDiscoveryTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Gets the total budget for comparison preparation.
    /// </summary>
    internal static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets the total budget for comparison freshness inspection.
    /// </summary>
    internal static readonly TimeSpan FreshnessTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Defines the maximum standard-output size for a comparison fact command.
    /// </summary>
    internal const int MaximumFactOutputBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     Defines the maximum standard-error size for a comparison fact command.
    /// </summary>
    internal const int MaximumDiagnosticBytes = 64 * 1024;

    /// <summary>
    ///     Defines the maximum encoded comparison-target page size.
    /// </summary>
    internal const int TargetPageBudgetBytes = 48 * 1024;

    /// <summary>
    ///     Defines the maximum Unicode scalar count for a target query.
    /// </summary>
    internal const int MaximumQueryScalars = 256;

    /// <summary>
    ///     Defines the maximum Unicode scalar count for a Git reference.
    /// </summary>
    internal const int MaximumRefScalars = 4_096;

    /// <summary>
    ///     Defines the lowercase hexadecimal length of a SHA-256 fingerprint.
    /// </summary>
    internal const int FingerprintHexLength = 64;

    /// <summary>
    ///     Defines the rename-similarity threshold used for comparison facts.
    /// </summary>
    internal const int RenameSimilarityPercent = 50;
}
