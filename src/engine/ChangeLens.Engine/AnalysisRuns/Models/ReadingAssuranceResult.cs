namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one publication assurance.
/// </summary>
/// <param name="Kind">The assurance kind wire value.</param>
/// <param name="Detail">The assurance detail.</param>
/// <param name="ClaimId">The claim the assurance addresses, or <see langword="null" /> when it addresses the whole model.</param>
internal sealed record ReadingAssuranceResult(string Kind, string Detail, string? ClaimId);
