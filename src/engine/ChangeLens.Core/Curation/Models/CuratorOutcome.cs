namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents the parsed draft and diagnostics from one curator call.
/// </summary>
/// <param name="Draft">The parsed draft, or an empty draft when parsing failed.</param>
/// <param name="Diagnostics">The provider, size, and parse diagnostics.</param>
public sealed record CuratorOutcome(MentalModelDraft Draft, CuratorDiagnostics Diagnostics);
