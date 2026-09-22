namespace ChangeLens.Core.EvidenceFrontier.Models;

/// <summary>
///     Represents evidence that the analysis ranked, disclosed, or bound but did not publish as a claim citation.
/// </summary>
/// <param name="Entries">The capped frontier entries in display order. Cannot be <see langword="null" />.</param>
/// <param name="Diagnostics">The pre-cap and returned-entry counts. Cannot be <see langword="null" />.</param>
public sealed record EvidenceFrontier(
    IReadOnlyList<FrontierEntry> Entries,
    FrontierDiagnostics Diagnostics);
