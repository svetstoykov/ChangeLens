using ChangeLens.Core.Comparisons.Models;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents one immutable comparison target in the engine protocol.
/// </summary>
/// <param name="Kind">The supported target kind.</param>
/// <param name="Name">The target display name.</param>
/// <param name="FullName">The full Git reference name.</param>
/// <param name="Revision">The resolved full object identifier.</param>
internal sealed record ComparisonTargetResult(
    ComparisonTargetKindResult Kind,
    string Name,
    string FullName,
    string Revision)
{
    /// <summary>
    ///     Maps a Core comparison target to its versioned protocol result.
    /// </summary>
    /// <param name="target">The comparison target to map. Cannot be <see langword="null" />.</param>
    /// <returns>The comparison-target protocol result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="target" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The target contains an unapproved comparison-target kind.
    /// </exception>
    internal static ComparisonTargetResult FromDescriptor(ComparisonTargetDescriptor target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var kind = target.Kind switch
        {
            ComparisonTargetKind.Local => ComparisonTargetKindResult.Local,
            ComparisonTargetKind.RemoteTracking => ComparisonTargetKindResult.RemoteTracking,
            _ => throw new InvalidOperationException(
                "The comparison target kind is not approved for the engine protocol."),
        };

        return new ComparisonTargetResult(
            kind,
            target.Name,
            target.FullName,
            target.Revision);
    }
}
