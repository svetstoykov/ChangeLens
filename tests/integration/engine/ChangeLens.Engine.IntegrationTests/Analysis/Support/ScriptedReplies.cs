using System.Text.Json;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Builds curator and reviewer replies that fit the binder a scripted pipeline run sends.
/// </summary>
internal static class ScriptedReplies
{
    /// <summary>
    ///     Wraps reply text in a successful completion.
    /// </summary>
    /// <param name="text">The reply text.</param>
    /// <returns>The successful completion result.</returns>
    internal static Result<ModelCompletionModel> Success(string text) =>
        Result.Success(new ModelCompletionModel("scripted-model", text, 1.0, 1, 1, null, null));

    /// <summary>
    ///     Builds a valid curator draft that cites the binder's marker node.
    /// </summary>
    /// <param name="request">The curator request whose binder supplies the node ids.</param>
    /// <returns>The successful curator completion.</returns>
    internal static Result<ModelCompletionModel> Curator(ModelCompletionRequest request) =>
        Success(CuratorJson(request, FindMarker(request).NodeId));

    /// <summary>
    ///     Builds a valid curator draft that cites one node in its thesis, track summary, participant, and step.
    /// </summary>
    /// <param name="request">The curator request.</param>
    /// <param name="citedNodeId">The node id every citation uses.</param>
    /// <returns>The curator draft JSON.</returns>
    internal static string CuratorJson(ModelCompletionRequest request, string citedNodeId)
    {
        var trackNodeId = citedNodeId;
        return JsonSerializer.Serialize(new
        {
            thesis = new { text = "The committed change matters.", evidenceNodeIds = new[] { citedNodeId } },
            tracks = new object[]
            {
                new
                {
                    id = "t1",
                    title = "Committed change",
                    summary = new { text = "The change is described.", evidenceNodeIds = new[] { trackNodeId } },
                    shape = "Walk",
                    participants = new object[]
                    {
                        new { id = "p1", name = "Participant", role = "Actor", changed = true, evidenceNodeIds = new[] { trackNodeId } },
                    },
                    relationships = Array.Empty<object>(),
                    orderedSteps = new object[] { new { text = "The first step.", evidenceNodeIds = new[] { trackNodeId } } },
                    purposes = Array.Empty<object>(),
                },
            },
            droppedNodeIds = Array.Empty<string>(),
        });
    }

    /// <summary>
    ///     Builds a reviewer reply from finding objects.
    /// </summary>
    /// <param name="findings">The finding objects, in reply order.</param>
    /// <returns>The successful reviewer completion.</returns>
    internal static Result<ModelCompletionModel> Reviewer(params object[] findings) =>
        Success(JsonSerializer.Serialize(new { findings }));

    /// <summary>
    ///     Builds a finding that passes validation: it cites the marker node and anchors on its marker line.
    /// </summary>
    /// <param name="request">The reviewer request whose binder supplies the marker node.</param>
    /// <param name="id">The finding id.</param>
    /// <param name="severity">The finding severity.</param>
    /// <returns>The finding object.</returns>
    internal static object ValidFinding(ModelCompletionRequest request, string id, string severity = "warning")
    {
        var marker = FindMarker(request);
        return new
        {
            id,
            severity,
            title = "The literal returned to callers changed",
            trigger = "A caller reads the value while it is the marker.",
            impact = "Callers observe the marker instead of the original value.",
            fix = "Restore the original literal.",
            evidenceNodeIds = new[] { marker.NodeId },
            anchor = new { nodeId = marker.NodeId, lines = marker.AnchorLine },
        };
    }

    /// <summary>
    ///     Builds a finding that passes validation and also cites another disclosed node.
    /// </summary>
    /// <param name="request">The reviewer request whose binder supplies the marker node and the other node.</param>
    /// <param name="id">The finding id.</param>
    /// <param name="otherPath">The path of the other cited node.</param>
    /// <returns>The finding object.</returns>
    internal static object FindingCiting(ModelCompletionRequest request, string id, string otherPath)
    {
        var marker = FindMarker(request);
        return new
        {
            id,
            severity = "warning",
            title = "The literal returned to callers changed",
            trigger = "A caller reads the value while it is the marker.",
            impact = "Callers observe the marker instead of the original value.",
            fix = "Restore the original literal.",
            evidenceNodeIds = new[] { marker.NodeId, FindNodeId(request, otherPath) },
            anchor = new { nodeId = marker.NodeId, lines = marker.AnchorLine },
        };
    }

    /// <summary>
    ///     Finds the id of the source quote a request's binder holds for a path on the after side.
    /// </summary>
    /// <param name="request">The request whose binder is searched.</param>
    /// <param name="path">The repository-relative path.</param>
    /// <returns>The node id of the first after-side quote of that path.</returns>
    internal static string FindNodeId(ModelCompletionRequest request, string path)
    {
        using var binder = JsonDocument.Parse(request.UserMessage);
        return binder.RootElement.GetProperty("evidence").EnumerateArray()
            .First(node => node.GetProperty("path").GetString() == path && node.GetProperty("side").GetString() == "After"
                && node.GetProperty("startLine").GetInt32() > 0)
            .GetProperty("nodeId").GetString()!;
    }

    /// <summary>
    ///     Builds a finding that validation removes because it cites an evidence node the binder never disclosed.
    /// </summary>
    /// <param name="id">The finding id.</param>
    /// <returns>The finding object.</returns>
    internal static object UndisclosedFinding(string id) =>
        new
        {
            id,
            severity = "warning",
            title = "A defect in a file the binder does not show",
            trigger = "Any call.",
            impact = "Unknown.",
            fix = "Unknown.",
            evidenceNodeIds = new[] { "undisclosed-node" },
            anchor = new { nodeId = "undisclosed-node", lines = "return" },
        };

    private static (string NodeId, string AnchorLine) FindMarker(ModelCompletionRequest request)
    {
        using var binder = JsonDocument.Parse(request.UserMessage);
        var evidence = binder.RootElement.GetProperty("evidence").EnumerateArray().ToArray();
        var node = evidence.First(candidate => Text(candidate).Contains("committed-marker", StringComparison.Ordinal));
        var line = Text(node).Split('\n').First(candidate => candidate.Contains("committed-marker", StringComparison.Ordinal));
        return (node.GetProperty("nodeId").GetString()!, line.TrimEnd('\r'));
    }

    private static string Text(JsonElement evidence) => evidence.GetProperty("text").GetString() ?? string.Empty;
}
