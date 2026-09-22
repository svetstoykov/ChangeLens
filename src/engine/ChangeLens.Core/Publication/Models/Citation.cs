using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Joins one published claim to one binder-held evidence node.</summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="NodeId">The evidence node identifier.</param>
/// <param name="Side">The comparison side.</param>
/// <param name="Path">The quoted path.</param>
/// <param name="ObjectId">The graph object identifier.</param>
/// <param name="StartLine">The full quote start line.</param>
/// <param name="EndLine">The full quote end line.</param>
/// <param name="Focus">The resolved checker focus ranges.</param>
/// <param name="Provenance">The citation provenance.</param>
public sealed record Citation(
    string ClaimId,
    string NodeId,
    ChangeAnatomySide Side,
    string Path,
    string ObjectId,
    int StartLine,
    int EndLine,
    IReadOnlyList<FocusRange> Focus,
    CitationProvenance Provenance);
