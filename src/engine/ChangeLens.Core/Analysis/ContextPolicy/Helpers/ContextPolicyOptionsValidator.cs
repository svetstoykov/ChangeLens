using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.EvidenceGraph.Models;

namespace ChangeLens.Core.Analysis.ContextPolicy.Helpers;

/// <summary>
///     Validates configured bounds for context policy disclosure.
/// </summary>
public static class ContextPolicyOptionsValidator
{
    /// <summary>
    ///     Validates context policy disclosure bounds.
    /// </summary>
    /// <param name="options">The context policy options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Any bound or share violates its invariant.</exception>
    public static void Validate(ContextPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumDisclosedCharactersPerNode <= 0
            || options.MinimumSecretValueLength <= 0
            || options.MaximumRedactedLineShare <= 0
            || options.MaximumRedactedLineShare > 1)
        {
            throw new InvalidOperationException("Context policy options must contain valid bounds.");
        }
    }

    /// <summary>
    ///     Validates context policy disclosure bounds together with the evidence graph quoted-line cap.
    /// </summary>
    /// <param name="graphOptions">The evidence graph options supplying the quoted-line cap. Cannot be <see langword="null" />.</param>
    /// <param name="options">The context policy options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="graphOptions" /> or <paramref name="options" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">Any bound or share violates its invariant.</exception>
    public static void Validate(EvidenceGraphOptions graphOptions, ContextPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(graphOptions);
        Validate(options);

        if (graphOptions.MaximumQuotedLinesPerNode <= 0)
        {
            throw new InvalidOperationException("Context policy options must contain valid bounds.");
        }
    }
}
