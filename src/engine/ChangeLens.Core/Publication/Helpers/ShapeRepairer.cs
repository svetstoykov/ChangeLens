using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Models;

namespace ChangeLens.Core.Publication.Helpers;

/// <summary>Repairs a track shape when its declared field is empty.</summary>
public static class ShapeRepairer
{
    /// <summary>Repairs all track shapes and returns the repairs in track order.</summary>
    /// <param name="model">The published mental model.</param>
    /// <returns>The repaired model and the applied repairs.</returns>
    public static (MentalModel Model, IReadOnlyList<ShapeRepair> Repairs) Repair(MentalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var repairs = new List<ShapeRepair>();
        var tracks = model.Tracks.Select(track => RepairTrack(track, repairs)).ToArray();
        return (new MentalModel(model.Thesis, tracks), repairs);
    }

    private static MentalModelTrack RepairTrack(MentalModelTrack track, ICollection<ShapeRepair> repairs)
    {
        var declared = track.Shape;
        var declaredPopulated = declared switch
        {
            "Walk" => track.OrderedSteps.Count > 0,
            "ParticipantMap" => track.Relationships.Count > 0,
            "PurposeCards" => track.Purposes.Count > 0,
            _ => false,
        };
        if (declaredPopulated)
        {
            return track;
        }

        var published = track.OrderedSteps.Count > 0
            ? "Walk"
            : track.Relationships.Count > 0
                ? "ParticipantMap"
                : track.Purposes.Count > 0
                    ? "PurposeCards"
                    : declared;
        if (!string.Equals(declared, published, StringComparison.Ordinal))
        {
            repairs.Add(new ShapeRepair(track.Id, declared, published));
            return track with { Shape = published };
        }

        return track;
    }
}
