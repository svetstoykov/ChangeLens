using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents the mental model produced after applying checker verdicts.
/// </summary>
/// <param name="Model">The checked mental model.</param>
/// <param name="Summary">The deterministic checking measurements.</param>
public sealed record ClaimCheckingOutcome(MentalModel Model, ClaimCheckingSummary Summary);
