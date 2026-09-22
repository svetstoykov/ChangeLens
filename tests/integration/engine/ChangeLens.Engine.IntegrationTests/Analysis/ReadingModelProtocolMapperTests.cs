using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Helpers;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the mapping from the published reading model to the engine protocol vocabulary.
/// </summary>
public sealed class ReadingModelProtocolMapperTests
{
    /// <summary>
    ///     Verifies closed-vocabulary values map to their camelCase wire spellings and preserve order.
    /// </summary>
    [Fact]
    public void ToProtocolMapsWireVocabularyAndPreservesFocusOrder()
    {
        var result = ReadingModelProtocolMapper.ToProtocol(CreateReadingModel());

        Assert.True(result.IsSuccess);
        var mapped = result.Data!;
        Assert.Equal(ReadingModelProtocolConstants.ShapeWalk, Assert.Single(mapped.Areas).Shape);
        Assert.Equal(ReadingModelProtocolConstants.TrustUnchecked, mapped.Thesis!.Trust);
        var citation = Assert.Single(mapped.Citations);
        Assert.Equal(ReadingModelProtocolConstants.SideAfter, citation.Side);
        Assert.Collection(
            mapped.Limitations,
            limitation => Assert.Equal(ReadingModelProtocolConstants.LimitationFileNotRead, limitation.Kind),
            limitation => Assert.Equal(ReadingModelProtocolConstants.LimitationFileNotQuoted, limitation.Kind),
            limitation => Assert.Equal(ReadingModelProtocolConstants.LimitationUncommittedWorkExcluded, limitation.Kind));
        Assert.Collection(
            citation.Focus,
            range => Assert.Equal(12, range.StartLine),
            range => Assert.Equal(16, range.StartLine));
    }

    /// <summary>
    ///     Verifies an unapproved removal scope fails the whole mapping with the stable error code.
    /// </summary>
    [Fact]
    public void ToProtocolRejectsUnknownRemovalScope()
    {
        var result = ReadingModelProtocolMapper.ToProtocol(
            new List<ValidationRemoval> { new("bogus", "thesis", "unknown scope") });

        Assert.Equal(AnalysisProtocolErrorCode.UnmappedReadingModel, Assert.Single(result.Errors).Code);
    }

    /// <summary>
    ///     Verifies an unapproved omission source kind fails the whole mapping with the stable error code.
    /// </summary>
    [Fact]
    public void ToProtocolRejectsUnknownOmissionSourceKind()
    {
        var model = CreateReadingModel() with
        {
            OmissionSummaries = [new ReadingOmissionSummary("budgetDropped", "over budget", 1, 1, 1)],
        };

        var result = ReadingModelProtocolMapper.ToProtocol(model);

        Assert.Equal(AnalysisProtocolErrorCode.UnmappedReadingModel, Assert.Single(result.Errors).Code);
    }

    private static ReadingModel CreateReadingModel() => new(
        new BinderComparison(
            Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab"),
            "/projects/change_lens",
            "refs/heads/feature/comparison",
            new string('a', 40),
            new string('b', 40),
            new string('c', 40),
            1720000000000,
            new ExcludedUncommittedCounts(0, 0, 0, 0, 0)),
        new ReadingStatement("thesis", "The parser rejects an empty name.", ReadingTrust.Unchecked, ["n1"]),
        [
            new ReadingArea(
                "parse",
                "Name check",
                null,
                ReadingShape.Walk,
                [],
                [],
                [new ReadingStatement("track:parse:step:0", "Reject the empty name.", ReadingTrust.Unchecked, ["n1"])],
                []),
        ],
        [
            new Citation(
                "track:parse:step:0",
                "n1",
                ChangeAnatomySide.After,
                "src/Name.cs",
                new string('d', 40),
                10,
                18,
                [new FocusRange("n1", 12, 12), new FocusRange("n1", 16, 17)],
                CitationProvenance.Unchecked),
        ],
        [new ReadingEvidence("n1", "src/Name.cs", ChangeAnatomySide.After, 10, 18, true, false, false, "public sealed class Name")],
        [
            new ReadingLimitation(ReadingLimitationKind.FileNotRead, "assets/blob.bin", "binary content"),
            new ReadingLimitation(ReadingLimitationKind.FileNotQuoted, "src/Name.cs", "not quoted"),
            new ReadingLimitation(ReadingLimitationKind.UncommittedWorkExcluded, null, "excluded"),
        ],
        [new ReadingOmissionSummary("fileNotRead", "binary content", 1, 1, 1)],
        [new ReadingAssurance(ReadingAssuranceKind.CheckerNotRun, "Claim checking was not run.")]);
}