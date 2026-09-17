namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents the disclosure decision for one evidence node.
/// </summary>
/// <param name="NodeId">The evidence node id. Cannot be <see langword="null" />.</param>
/// <param name="Path">The repository-relative path of the node. Cannot be <see langword="null" />.</param>
/// <param name="Verdict">The disclosure verdict.</param>
/// <param name="ExclusionReason">A short exclusion reason for <see cref="ContextPolicyVerdict.Exclude" />; otherwise <see langword="null" />.</param>
/// <param name="Redactions">
///     The redacted lines for <see cref="ContextPolicyVerdict.Redact" />; otherwise empty. Cannot be <see langword="null" />.
/// </param>
public sealed record ContextPolicyDecision(
    string NodeId,
    string Path,
    ContextPolicyVerdict Verdict,
    string? ExclusionReason,
    IReadOnlyList<ContextPolicyRedaction> Redactions);
