using System.Text.Json;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies deterministic serializer-aware comparison-target page shaping.
/// </summary>
public sealed class ComparisonTargetPageBuilderTests
{
    private const int TargetPageBudgetBytes = 48 * 1024;
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string Token =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly EngineProtocolSerializer _serializer = new();

    /// <summary>
    ///     Verifies the Engine-owned complete-response ceiling remains exactly 48 KiB.
    /// </summary>
    [Fact]
    public void TargetPageBudgetIsExactly48KiB()
    {
        Assert.Equal(TargetPageBudgetBytes, ComparisonActionConstants.TargetPageBudgetBytes);
    }

    /// <summary>
    ///     Verifies target order, target kinds, and the suggested target are preserved.
    /// </summary>
    [Fact]
    public void BuildPreservesCoreTargetOrderAndMapsKinds()
    {
        var local = Target(ComparisonTargetKind.Local, "topic", "refs/heads/topic");
        var remote = Target(
            ComparisonTargetKind.RemoteTracking,
            "origin/main",
            "refs/remotes/origin/main");
        var targetSet = new ComparisonTargetSet([local, remote], remote, Token, 2);

        var result = CreateBuilder().Build("request-order", targetSet);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["refs/heads/topic", "refs/remotes/origin/main"],
            result.Data!.Targets.Select(target => target.FullName));
        Assert.Equal(
            [ComparisonTargetKindResult.Local, ComparisonTargetKindResult.RemoteTracking],
            result.Data.Targets.Select(target => target.Kind));
        Assert.Equal("refs/remotes/origin/main", result.Data.SuggestedTarget!.FullName);
        Assert.Null(result.Data.NextCursor);
        Assert.Equal(2, result.Data.UnsupportedTargetCount);
    }

    /// <summary>
    ///     Verifies an empty Core target set produces a successful empty protocol page.
    /// </summary>
    [Fact]
    public void BuildReturnsSuccessfulEmptyPage()
    {
        var result = CreateBuilder().Build(
            "request-empty",
            new ComparisonTargetSet([], null, Token, 3));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Targets);
        Assert.Null(result.Data.SuggestedTarget);
        Assert.Null(result.Data.NextCursor);
        Assert.Equal(Token, result.Data.TargetSetToken);
        Assert.Equal(3, result.Data.UnsupportedTargetCount);
    }

    /// <summary>
    ///     Verifies escaped request-identifier overhead is included for empty and all-unsupported pages.
    /// </summary>
    /// <param name="includeUnsupportedTarget">
    ///     <see langword="true" /> to shape an all-unsupported page; otherwise, shape an empty page.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildRejectsFinalEnvelopeBeyondBudget(bool includeUnsupportedTarget)
    {
        var boundaryLength = FindLargestEscapedRequestIdLengthWithinBudget();
        var withinBudgetRequestId = "request-" + new string('\u0001', boundaryLength);
        var beyondBudgetRequestId = withinBudgetRequestId + "\u0001";
        IReadOnlyList<ComparisonTargetDescriptor> targets = includeUnsupportedTarget
            ? [
                Target(
                    ComparisonTargetKind.Local,
                    string.Concat(Enumerable.Repeat("😀", 4_080)),
                    "refs/heads/" + string.Concat(Enumerable.Repeat("😀", 4_080))),
            ]
            : [];

        var withinBudget = CreateBuilder().Build(
            withinBudgetRequestId,
            new ComparisonTargetSet([], null, Token, 0));
        var beyondBudget = CreateBuilder().Build(
            beyondBudgetRequestId,
            new ComparisonTargetSet(targets, null, Token, 0));

        Assert.True(withinBudget.IsSuccess);
        Assert.True(
            Measure(withinBudgetRequestId, withinBudget.Data!) <= TargetPageBudgetBytes);
        var error = Assert.Single(beyondBudget.Errors);
        Assert.Equal("comparison.tooLarge", error.Code);
    }

    /// <summary>
    ///     Verifies request overhead cannot reclassify a supported target on only one page.
    /// </summary>
    [Fact]
    public void BuildRejectsRequestEnvelopeThatCannotFitFirstSupportedTarget()
    {
        var boundaryLength = FindLargestEscapedRequestIdLengthWithinBudget();
        var requestId = "request-" + new string('\u0001', boundaryLength);
        var target = Target(ComparisonTargetKind.Local, "small", "refs/heads/small");

        var result = CreateBuilder().Build(
            requestId,
            new ComparisonTargetSet([target], null, Token, 3));

        var error = Assert.Single(result.Errors);
        Assert.Equal("comparison.tooLarge", error.Code);
    }

    /// <summary>
    ///     Verifies a descriptor that cannot fit by itself is omitted and counted without exposing its name.
    /// </summary>
    [Fact]
    public void BuildSkipsIndividuallyOversizedDescriptor()
    {
        var oversizedName = string.Concat(Enumerable.Repeat("😀", 4_080));
        var oversized = Target(
            ComparisonTargetKind.Local,
            oversizedName,
            "refs/heads/" + oversizedName);
        var supported = Target(ComparisonTargetKind.Local, "small", "refs/heads/small");

        var result = CreateBuilder().Build(
            "request-oversized",
            new ComparisonTargetSet([oversized, supported], null, Token, 4));

        Assert.True(result.IsSuccess);
        var emitted = Assert.Single(result.Data!.Targets);
        Assert.Equal(supported.FullName, emitted.FullName);
        Assert.Null(result.Data.NextCursor);
        Assert.Equal(5, result.Data.UnsupportedTargetCount);
        var json = Serialize("request-oversized", result.Data);
        Assert.DoesNotContain(oversizedName, json, StringComparison.Ordinal);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= TargetPageBudgetBytes);
    }

    /// <summary>
    ///     Verifies global unsupported classification includes the candidate continuation cursor.
    /// </summary>
    [Fact]
    public void BuildSkipsDescriptorThatCannotFitWithItsCursor()
    {
        var cursorHeavy = Target(
            ComparisonTargetKind.Local,
            "cursor-heavy",
            "refs/heads/" + new string('\u0001', 4_085));
        var supported = Target(ComparisonTargetKind.Local, "small", "refs/heads/small");

        var result = CreateBuilder().Build(
            "request-cursor-heavy",
            new ComparisonTargetSet([cursorHeavy, supported], null, Token, 0));

        Assert.True(result.IsSuccess);
        var emitted = Assert.Single(result.Data!.Targets);
        Assert.Equal(supported.FullName, emitted.FullName);
        Assert.Null(result.Data.NextCursor);
        Assert.Equal(1, result.Data.UnsupportedTargetCount);
    }

    /// <summary>
    ///     Verifies the complete correlated response stays in budget and cursors identify the last emitted target.
    /// </summary>
    [Fact]
    public void BuildMeasuresCompleteCorrelatedEnvelope()
    {
        var targets = Enumerable.Range(0, 180)
            .Select(
                index =>
                {
                    var name = $"{index:D3}-quote-\"-slash-\\-unicode-λ-" + new string('x', 180);
                    return Target(ComparisonTargetKind.Local, name, "refs/heads/" + name);
                })
            .ToArray();

        var result = CreateBuilder().Build(
            "request-capacity-identifier-with-real-envelope-cost",
            new ComparisonTargetSet(targets, null, Token, 0));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Data!.Targets);
        Assert.True(result.Data.Targets.Count < targets.Length);
        Assert.Equal(result.Data.Targets[^1].FullName, result.Data.NextCursor);
        var json = Serialize("request-capacity-identifier-with-real-envelope-cost", result.Data);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= TargetPageBudgetBytes);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    /// <summary>
    ///     Verifies repeated page construction neither duplicates nor omits supported candidates.
    /// </summary>
    [Fact]
    public void BuildProducesCompleteNonOverlappingPages()
    {
        var targets = Enumerable.Range(0, 320)
            .Select(
                index =>
                {
                    var escaped = (index % 3) switch
                    {
                        0 => "\"quoted\"",
                        1 => "back\\slash",
                        _ => "unicode-κόσμε",
                    };
                    var name = $"{index:D3}-{escaped}-" + new string('z', 120);
                    return Target(
                        index % 2 == 0
                            ? ComparisonTargetKind.Local
                            : ComparisonTargetKind.RemoteTracking,
                        name,
                        index % 2 == 0
                            ? "refs/heads/" + name
                            : "refs/remotes/origin/" + name);
                })
            .ToArray();
        var remaining = (IReadOnlyList<ComparisonTargetDescriptor>)targets;
        var emitted = new List<string>();

        while (remaining.Count > 0)
        {
            var result = CreateBuilder().Build(
                "request-repeat",
                new ComparisonTargetSet(remaining, null, Token, 0));
            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Data!.Targets);
            emitted.AddRange(result.Data.Targets.Select(target => target.FullName));
            var json = Serialize("request-repeat", result.Data);
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= TargetPageBudgetBytes);
            using var parsed = JsonDocument.Parse(json);

            if (result.Data.NextCursor is null)
            {
                break;
            }

            var cursorIndex = Array.FindIndex(
                targets,
                target => target.FullName == result.Data.NextCursor);
            remaining = targets.Skip(cursorIndex + 1).ToArray();
        }

        Assert.Equal(targets.Select(target => target.FullName), emitted);
        Assert.Equal(emitted.Count, emitted.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Verifies serializer-unsupported targets contribute one stable global count on every continuation page.
    /// </summary>
    [Fact]
    public void BuildKeepsSerializerUnsupportedCountAcrossContinuationPages()
    {
        var oversizedName = string.Concat(Enumerable.Repeat("😀", 4_080));
        var oversized = Target(
            ComparisonTargetKind.Local,
            oversizedName,
            "refs/heads/" + oversizedName);
        var targets = Enumerable.Range(0, 420)
            .Select(
                index =>
                {
                    var name = $"{index:D3}-" + new string('x', 150);
                    return Target(ComparisonTargetKind.Local, name, "refs/heads/" + name);
                })
            .Take(180)
            .Append(oversized)
            .Concat(
                Enumerable.Range(180, 240)
                    .Select(
                        index =>
                        {
                            var name = $"{index:D3}-" + new string('x', 150);
                            return Target(
                                ComparisonTargetKind.Local,
                                name,
                                "refs/heads/" + name);
                        }))
            .ToArray();
        var remaining = (IReadOnlyList<ComparisonTargetDescriptor>)targets;
        var emitted = new List<string>();
        var pageCount = 0;

        while (remaining.Count > 0)
        {
            var result = CreateBuilder().Build(
                "request-stable-count",
                new ComparisonTargetSet(remaining, null, Token, 3)
                {
                    UnpagedTargets = targets,
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(4, result.Data!.UnsupportedTargetCount);
            emitted.AddRange(result.Data.Targets.Select(target => target.FullName));
            pageCount++;

            if (result.Data.NextCursor is null)
            {
                break;
            }

            var cursorIndex = Array.FindIndex(
                targets,
                target => target.FullName == result.Data.NextCursor);
            remaining = targets.Skip(cursorIndex + 1).ToArray();
        }

        Assert.True(pageCount >= 3);
        Assert.DoesNotContain(oversized.FullName, emitted);
        Assert.Equal(
            targets.Where(target => target != oversized).Select(target => target.FullName),
            emitted);
    }

    /// <summary>
    ///     Verifies JSON metacharacters, Unicode, and control characters remain complete escaped JSON values.
    /// </summary>
    [Fact]
    public void BuildPreservesEscapedAndUnicodeTargetText()
    {
        const string escapedName = "quote-\"-slash-\\-newline-\n-tab-\t-control-\u0001-unicode-λ";
        var target = Target(
            ComparisonTargetKind.Local,
            escapedName,
            "refs/heads/" + escapedName);

        var result = CreateBuilder().Build(
            "request-escaped-\u0001",
            new ComparisonTargetSet([target], null, Token, 0));

        Assert.True(result.IsSuccess);
        var json = Serialize("request-escaped-\u0001", result.Data!);
        using var document = JsonDocument.Parse(json);
        var emitted = document.RootElement
            .GetProperty("result")
            .GetProperty("targets")[0];
        Assert.Equal(escapedName, emitted.GetProperty("name").GetString());
        Assert.Equal(target.FullName, emitted.GetProperty("fullName").GetString());
        Assert.Contains("\\u0001", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\\n", json, StringComparison.Ordinal);
        Assert.Contains("\\t", json, StringComparison.Ordinal);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= TargetPageBudgetBytes);
    }

    private ComparisonTargetPageBuilder CreateBuilder() => new(_serializer);

    private string Serialize(string requestId, ComparisonTargetPageResult result)
    {
        var response = ProtocolResponseFactory.CreateWithValue(requestId, result);
        var serialization = _serializer.SerializeResponse(response);
        Assert.True(serialization.IsSuccess);
        return serialization.Data!;
    }

    private int Measure(string requestId, ComparisonTargetPageResult result)
    {
        var response = ProtocolResponseFactory.CreateWithValue(requestId, result);
        var measurement = _serializer.GetSerializedUtf8ByteCount(response);
        Assert.True(measurement.IsSuccess);
        return measurement.Data;
    }

    private int FindLargestEscapedRequestIdLengthWithinBudget()
    {
        var low = 0;
        var high = 10_000;
        var page = new ComparisonTargetPageResult([], null, null, Token, 0);

        while (low < high)
        {
            var midpoint = low + (high - low + 1) / 2;
            var requestId = "request-" + new string('\u0001', midpoint);
            if (Measure(requestId, page) <= TargetPageBudgetBytes)
            {
                low = midpoint;
            }
            else
            {
                high = midpoint - 1;
            }
        }

        return low;
    }

    private static ComparisonTargetDescriptor Target(
        ComparisonTargetKind kind,
        string name,
        string fullName) =>
        new(kind, name, fullName, Revision);
}
