namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the durable renderable projection of a successfully completed analysis run.
/// </summary>
/// <remarks>
///     Both documents are engine protocol JSON. Infrastructure stores and returns them as opaque text; only the
///     Engine protocol boundary writes and parses them.
/// </remarks>
/// <param name="ReadingModelJson">The protocol JSON of the published reading model. Cannot be <see langword="null" />.</param>
/// <param name="ValidationRemovalsJson">
///     The protocol JSON array of the items mechanical draft validation removed. Cannot be <see langword="null" />.
/// </param>
public sealed record AnalysisReadingProjection(string ReadingModelJson, string ValidationRemovalsJson);
