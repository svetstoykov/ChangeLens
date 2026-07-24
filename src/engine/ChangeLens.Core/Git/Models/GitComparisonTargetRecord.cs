namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents one strictly parsed Git reference record used for comparison-target discovery.
/// </summary>
/// <param name="FullName">The full Git reference name. Cannot be <see langword="null" />.</param>
/// <param name="Revision">The resolved full object identifier. Cannot be <see langword="null" />.</param>
/// <param name="ObjectType">The resolved Git object type. Cannot be <see langword="null" />.</param>
/// <param name="SymbolicTarget">
///     The symbolic target's full reference, or <see langword="null" /> for a direct reference.
/// </param>
/// <param name="UpstreamRemote">
///     The configured upstream remote name, or <see langword="null" /> when none is configured.
/// </param>
internal sealed record GitComparisonTargetRecord(
    string FullName,
    string Revision,
    string ObjectType,
    string? SymbolicTarget,
    string? UpstreamRemote);
