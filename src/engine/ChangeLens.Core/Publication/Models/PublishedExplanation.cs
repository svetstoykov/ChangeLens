using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Holds the repaired, optionally checked explanation so a reading model can be rebuilt without checking again.</summary>
/// <param name="Model">The repaired mental model.</param>
/// <param name="Citations">The citations of the explanation's claims.</param>
/// <param name="Facts">The checker facts of the publication that produced the explanation.</param>
public sealed record PublishedExplanation(MentalModel Model, IReadOnlyList<Citation> Citations, CheckerFacts Facts);
