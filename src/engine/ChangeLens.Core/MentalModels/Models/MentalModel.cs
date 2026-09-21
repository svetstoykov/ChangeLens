namespace ChangeLens.Core.MentalModels.Models;

/// <summary>
///     Represents the published mental model consumed by reading-model publication.
/// </summary>
/// <param name="Thesis">The published or derived thesis, or <see langword="null" />.</param>
/// <param name="Tracks">The ordered published tracks.</param>
public sealed record MentalModel(MentalModelStatement? Thesis, IReadOnlyList<MentalModelTrack> Tracks)
{
    /// <summary>
    ///     Gets an empty published mental model.
    /// </summary>
    public static MentalModel Empty => new(null, []);
}
