namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents the parsed checker response.
/// </summary>
/// <param name="Verdicts">The verdicts returned by the checker.</param>
/// <param name="ParseFailure">The parse failure detail, or <see langword="null" />.</param>
public sealed record ClaimCheckerReply(IReadOnlyList<ClaimVerdict> Verdicts, string? ParseFailure);
