using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.EvidenceFrontier.Models;
using EvidenceFrontierModel = ChangeLens.Core.Analysis.EvidenceFrontier.Models.EvidenceFrontier;

namespace ChangeLens.Core.Analysis.Publication.Models;

/// <summary>Represents the complete deterministic publication result.</summary>
/// <param name="ReadingModel">The transferable reading model.</param>
/// <param name="EvidenceFrontier">The omitted evidence frontier.</param>
/// <param name="ShapeRepairs">The shape repairs applied during publication.</param>
/// <param name="Explanation">The published explanation, from which a reading model can be rebuilt without checking again.</param>
/// <param name="ClaimCheckingSummary">The checker summary, when checking ran.</param>
public sealed record PublicationOutcome(
    ReadingModel ReadingModel,
    EvidenceFrontierModel EvidenceFrontier,
    IReadOnlyList<ShapeRepair> ShapeRepairs,
    PublishedExplanation Explanation,
    ClaimCheckingSummary? ClaimCheckingSummary);
