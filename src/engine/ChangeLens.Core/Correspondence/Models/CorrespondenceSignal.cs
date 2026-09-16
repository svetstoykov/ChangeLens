namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents one scored reason that ties a correspondence candidate to a changed file.
/// </summary>
/// <remarks>
///     A signal directs attention. It is not evidence of a dependency, an invocation, or any other behavioral
///     relationship.
/// </remarks>
/// <param name="Kind">The signal family that received the contribution.</param>
/// <param name="ChangedPath">The current repository-relative path of the changed file. Cannot be <see langword="null" />.</param>
/// <param name="Contribution">The positive amount this signal added to the candidate score.</param>
public abstract record CorrespondenceSignal(CorrespondenceSignalKind Kind, string ChangedPath, double Contribution);
