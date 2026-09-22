using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Helpers;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Publication;

/// <summary>Verifies deterministic publication shape repair.</summary>
public sealed class ShapeRepairTests
{
    /// <summary>Verifies a declared empty participant map is repaired to purpose cards.</summary>
    [Fact]
    public void EmptyDeclaredParticipantMapWithPurposesPublishesPurposeCards()
    {
        var model = new MentalModel(null, [new MentalModelTrack(
            "track", "Track", null, CuratorContractConstants.ParticipantMap, [], [], [],
            [new MentalModelStatement("purpose", "Purpose", [], [])])]);

        var (repaired, repairs) = ShapeRepairer.Repair(model);

        Assert.Equal(CuratorContractConstants.PurposeCards, repaired.Tracks[0].Shape);
        var repair = Assert.Single(repairs);
        Assert.Equal(CuratorContractConstants.ParticipantMap, repair.Declared);
        Assert.Equal(CuratorContractConstants.PurposeCards, repair.Published);
    }

    /// <summary>Verifies a populated declared field is retained.</summary>
    [Fact]
    public void PopulatedDeclaredShapeIsLeftAlone()
    {
        var model = new MentalModel(null, [new MentalModelTrack(
            "track", "Track", null, CuratorContractConstants.ParticipantMap, [],
            [new MentalModelRelationship(
                "relationship", "relationship", "from", "to", CuratorContractConstants.Invokes, "Explanation", [], [], [])],
            [], [])]);

        var (repaired, repairs) = ShapeRepairer.Repair(model);

        Assert.Equal(CuratorContractConstants.ParticipantMap, repaired.Tracks[0].Shape);
        Assert.Empty(repairs);
    }
}
