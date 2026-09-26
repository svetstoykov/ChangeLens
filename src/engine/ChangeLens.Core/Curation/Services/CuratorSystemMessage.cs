using System.Text;
using ChangeLens.Core.EvidenceBinder.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Curation.Services;

/// <summary>
///     Renders the complete system instruction for one binder-bound curator call.
/// </summary>
public static class CuratorSystemMessage
{
    private const string BinderLegend = """
        comparison        The frozen revision pair this change lives in. Identity, not evidence.
        changedFiles      Every path the commit touched, and the evidence nodes quoting each one.
                          originalPath is the path before a rename; beforeMode and afterMode are the
                          file modes on each side, and differing modes are themselves a change.
        evidence[]        Your only proof. Each entry is a verbatim quote from a pinned blob, with
                          nodeId, path, side (Before or After), startLine, endLine, and changedFile.
                          A node whose id starts with "m" is a manifest fact rather than a quote: a
                          rename or a mode change has no lines to quote, and this is how such a change
                          becomes citable at all. It proves exactly what it states and nothing more.
        matchEdges[]      Not in this payload. Retrieval associations stay on the binder object for
                          checking; do not invent edge ids. Omit matchEdgeIds unless a disclosed edge
                          is available for a supersedes relationship.
        orientation       Sibling paths near the change, for naming and placement. Not evidence.
        developerContext  A human hint about intent. Not evidence, and it may be wrong.
        omissions         What earlier stages withheld. It explains gaps; it never fills them.
        contract          The closed kind list, shape list, and limits, machine-readable. The
                          meanings, rules, and output schema are in this message only.
        """;

    private const string WorkingMethod = """
        1. Read the changed hunks first. They are the subject; unchanged quotes exist to explain them.
        2. Group what the quotes show into tracks. One track tells one coherent story about the change.
        3. Name a participant only when a quote shows it, and give it the quotes that identify it.
        4. Assert a relationship only when quotes on both sides show that kind in that direction.
        5. Write the thesis last, from whatever survived steps 1 to 4.

        SPLITTING TRACKS — step 2 is the hardest judgment call, so apply the splitting-tracks test directly. If
        explaining one group of quotes requires citing evidence that only makes sense in service of
        another group, they are one track. If each group could stand alone as a complete,
        evidence-backed story without the other, they are separate tracks — split them even when they
        came from the same commit. A commit touching five files for one reason is one track. A commit
        touching two files for two unrelated reasons is two.
        """;

    private const string ParticipantsGuidance = """
        name     What a quote shows the participant to be: a function, class, type, route, or config key.
        role     One short phrase: what it does in this change. Cite the node that names it.
        changed  true if the participant appears in a changedFile path; false if it is context only.
        A participant with no evidence node is a name with no citation — never emit one.
        """;

    private static readonly IReadOnlyList<string> Rules =
    [
        "Explain this committed change using only the quoted text in evidence[].",
        "orientation, changedFiles, developerContext, and omissions direct attention. They prove nothing about behavior.",
        "Every statement and every relationship must cite evidence[].nodeId values that appear in this binder.",
        "A relationship must cite at least one evidence node held by its from participant and at least one held by its to participant. "
        + "Repeat a shared endpoint's node id on every relationship that touches it — citing it once does not carry to the rest.",
        "Use only the relationship kinds listed above, and only when the quotes show that kind in that direction.",
        "A supersedes relationship additionally requires a matchEdges chain linking the two participants' evidence.",
        "Never invent a path, a line number, a node ID, a participant, or a causal link.",
        "Omit whatever the quotes do not show. A short supported model is the goal; a complete speculative one is a failure.",
        "Call something added, new, introduced, or gained only when no Before quote shows it. When a Before quote shows the same declaration, "
        + "say what changed between the Before and After quotes and cite both. This applies to the thesis too.",
        "List every binder node you deliberately did not use in droppedNodeIds.",
    ];

    private const string OutputSchema = """
        {
          "thesis": { "text": "<one-sentence editorial overview of this draft>", "evidenceNodeIds": ["<nodeId>"] },
          "tracks": [
            {
              "id": "<id>",
              "title": "<short title>",
              "summary": { "text": "<what this track explains>", "evidenceNodeIds": ["<nodeId>"] },
              "shape": "Walk | ParticipantMap | PurposeCards",
              "participants": [
                { "id": "<id>", "name": "<name a quote shows>", "role": "<what it does>", "changed": true, "evidenceNodeIds": ["<nodeId>"] }
              ],
              "relationships": [
                { "id": "<id>", "fromParticipantId": "<id>", "toParticipantId": "<id>", "kind": "<allowed kind>",
                  "explanation": "<why the quotes show it>", "evidenceNodeIds": ["<nodeId>"], "matchEdgeIds": ["<edgeId>"] }
              ],
              "orderedSteps": [{ "text": "<step>", "evidenceNodeIds": ["<nodeId>"] }],
              "purposes": [{ "text": "<purpose>", "evidenceNodeIds": ["<nodeId>"] }]
            }
          ],
          "droppedNodeIds": ["<nodeId>"]
        }
        """;

    private const string WorkedExample = """
        binder evidence (abbreviated):
          n001  After   src/route.cs         12   app.MapPost("/orders", CreateOrder);
          n002  After   src/service.cs        8   public void CreateOrder(Order o) { _repo.Save(o); }
          n003  After   src/service.cs       30   public void Save(Order o) { _db.Add(o); }
          n004  After   src/appsettings.json  5   "RetryCount": 3,
          n005  After   src/appsettings.json  6   "RetryDelayMs": 250

        output:
          {
            "thesis": { "text": "The route delegates to CreateOrder, which persists through Save; the outbound retry policy was tuned in the same commit.", "evidenceNodeIds": ["n001","n002","n003","n004","n005"] },
            "tracks": [
              {
                "id": "order-flow", "title": "Route to persistence",
                "summary": { "text": "A POST route hands off to CreateOrder, which calls Save.", "evidenceNodeIds": ["n001","n002","n003"] },
                "shape": "Walk",
                "participants": [
                  { "id": "route", "name": "MapPost route", "role": "entry point", "changed": true, "evidenceNodeIds": ["n001"] },
                  { "id": "handler", "name": "CreateOrder", "role": "order handler", "changed": true, "evidenceNodeIds": ["n002"] },
                  { "id": "repo", "name": "Save", "role": "persistence", "changed": false, "evidenceNodeIds": ["n003"] }
                ],
                "relationships": [
                  { "id": "r1", "fromParticipantId": "route", "toParticipantId": "handler", "kind": "invokes", "explanation": "The route maps to CreateOrder.", "evidenceNodeIds": ["n001","n002"], "matchEdgeIds": [] },
                  { "id": "r2", "fromParticipantId": "handler", "toParticipantId": "repo", "kind": "invokes", "explanation": "CreateOrder calls Save.", "evidenceNodeIds": ["n002","n003"], "matchEdgeIds": [] }
                ],
                "orderedSteps": [
                  { "text": "The route receives the POST.", "evidenceNodeIds": ["n001"] },
                  { "text": "CreateOrder calls Save.", "evidenceNodeIds": ["n002"] }
                ],
                "purposes": []
              },
              {
                "id": "retry-policy", "title": "Retry policy tuning",
                "summary": { "text": "The outbound retry policy was tuned independently of the order route.", "evidenceNodeIds": ["n004","n005"] },
                "shape": "PurposeCards",
                "participants": [
                  { "id": "retryConfig", "name": "appsettings.json retry policy", "role": "runtime configuration", "changed": true, "evidenceNodeIds": ["n004","n005"] }
                ],
                "relationships": [],
                "orderedSteps": [],
                "purposes": [
                  { "text": "Raises RetryCount so more attempts survive a transient failure.", "evidenceNodeIds": ["n004"] },
                  { "text": "Adds RetryDelayMs so retries do not immediately hammer a struggling dependency.", "evidenceNodeIds": ["n005"] }
                ]
              }
            ],
            "droppedNodeIds": []
          }
        Each relationship cites at least one node from each endpoint's evidence. matchEdgeIds is
        omitted here because no relationship is supersedes — that kind alone requires it.

        Worked hub example — a shared endpoint with many participants pointing at one. Extra binder evidence:
          n010  After   src/diagnostics.cs  1   public abstract class DiagnosticModule { ... }
          n011  After   src/alpha.cs        4   class Alpha : DiagnosticModule
          n012  After   src/beta.cs         4   class Beta : DiagnosticModule

          {
            "thesis": { "text": "Alpha and Beta both depend on the shared diagnostic module.", "evidenceNodeIds": ["n010","n011","n012"] },
            "tracks": [{
              "id": "diagnostics-hub", "title": "Diagnostic module adopters",
              "summary": { "text": "Two classes derive from one shared diagnostic module.", "evidenceNodeIds": ["n010","n011","n012"] },
              "shape": "ParticipantMap",
              "participants": [
                { "id": "alpha", "name": "Alpha", "role": "diagnostic module adopter", "changed": true, "evidenceNodeIds": ["n011"] },
                { "id": "beta", "name": "Beta", "role": "diagnostic module adopter", "changed": true, "evidenceNodeIds": ["n012"] },
                { "id": "base", "name": "DiagnosticModule", "role": "shared diagnostic abstraction", "changed": true, "evidenceNodeIds": ["n010"] }
              ],
              "relationships": [
                { "id": "r3", "fromParticipantId": "alpha", "toParticipantId": "base", "kind": "depends-on", "explanation": "Alpha derives from DiagnosticModule.", "evidenceNodeIds": ["n011","n010"], "matchEdgeIds": [] },
                { "id": "r4", "fromParticipantId": "beta", "toParticipantId": "base", "kind": "depends-on", "explanation": "Beta derives from DiagnosticModule.", "evidenceNodeIds": ["n012","n010"], "matchEdgeIds": [] }
              ],
              "orderedSteps": [],
              "purposes": []
            }],
            "droppedNodeIds": []
          }
        n010 appears on both relationships, and would appear on all twenty if twenty classes derived from it.
        Every relationship is checked alone: citing the shared end once on r3 proves nothing about r4, and a
        relationship naming only its own end is discarded whole. Quoting the derived class does not stand in
        for the base — name both node ids, every time.

        Why two tracks and not one: nothing in the retry-policy quotes is needed to explain the order-flow
        quotes, or the reverse — each stands alone as a complete, evidence-backed story, so SPLITTING TRACKS
        puts them apart even though the commit is one diff. retry-policy uses PurposeCards because its
        explanation lives in independent reasons, not a path (Walk) or a relationship graph (ParticipantMap)
        — the shape follows whichever field actually carries the story.
        """;

    /// <summary>
    ///     Renders the system instruction from the binder contract.
    /// </summary>
    /// <param name="binder">The binder whose contract limits the instruction. Cannot be <see langword="null" />.</param>
    /// <returns>The system instruction used for the completion request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder" /> is <see langword="null" />.</exception>
    public static string Render(EvidenceBinderModel binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return Render(binder.Contract);
    }

    /// <summary>
    ///     Renders the system instruction from a configured curator contract.
    /// </summary>
    /// <param name="contract">The closed curator contract. Cannot be <see langword="null" />.</param>
    /// <returns>The system instruction used for the completion request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contract" /> is <see langword="null" />.</exception>
    public static string Render(BinderContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Limits);

        var builder = new StringBuilder();
        builder.AppendLine(
            "You are the curator in ChangeLens. You receive one JSON binder describing a single committed software change, and you return one JSON "
            + "object: a mental model of that change in which every statement is bound to frozen evidence.");
        builder.AppendLine(
            "This is one JSON completion with no tools or tool calls. The binder is the complete evidence offer; never ask for expansion or invent "
            + "facts.");
        builder.AppendLine();
        builder.AppendLine("WHAT THE BINDER CONTAINS");
        builder.AppendLine(BinderLegend);
        builder.AppendLine();
        builder.AppendLine("HOW TO WORK");
        builder.AppendLine(WorkingMethod);
        builder.AppendLine();
        builder.AppendLine("PARTICIPANTS");
        builder.AppendLine(ParticipantsGuidance);
        builder.AppendLine();
        builder.AppendLine("RELATIONSHIP KINDS — use these and nothing else. A is the `from` participant, B is the `to` participant.");
        AppendRelationshipKinds(builder, contract.RelationshipKinds);
        builder.AppendLine(
            "NOTE: `supersedes` is the only kind where `to` (B) is the actor — B replaces A. Every other kind has `from` (A) acting on `to` "
            + "(B).");
        builder.AppendLine();
        builder.AppendLine("TRACK SHAPES — set `shape` to the one whose field carries your explanation.");
        AppendTrackShapes(builder, contract.TrackShapes);
        builder.AppendLine("Use one track per story. Split into multiple tracks when the change has more than one independent story.");
        builder.AppendLine();
        builder.AppendLine("RULES");
        for (var index = 0; index < Rules.Count; index++)
        {
            builder.Append(index + 1).Append(". ").AppendLine(Rules[index]);
        }

        builder.AppendLine();
        builder.AppendLine("LIMITS — anything past these is discarded before it is read.");
        builder.Append("  tracks                        at most ").AppendLine(contract.Limits.MaximumTracks.ToString());
        builder.Append("  participants per track        at most ").AppendLine(contract.Limits.MaximumParticipantsPerTrack.ToString());
        builder.Append("  relationships per track       at most ").AppendLine(contract.Limits.MaximumRelationshipsPerTrack.ToString());
        builder.Append("  orderedSteps, purposes        at most ").Append(contract.Limits.MaximumItemsPerTrack + " each");
        builder.AppendLine();
        builder.Append("  statements, explanations      at most ").Append(contract.Limits.MaximumStatementCharacters).AppendLine(" characters");
        builder.Append("  every id                      ").AppendLine(contract.Limits.IdFormat);
        builder.AppendLine();
        builder.AppendLine("OUTPUT");
        builder.AppendLine("Return exactly one JSON object. No markdown fence, no prose before or after it. Shape:");
        builder.AppendLine(OutputSchema);
        builder.AppendLine(
            "`thesis` is the editorial overview of this draft and remains visible in curation diagnostics. The published thesis is derived later "
            + "from whichever track summaries survive checking.");
        builder.AppendLine(
            "Omit `orderedSteps`, `purposes`, `relationships`, or `matchEdgeIds` rather than emitting an empty placeholder. If the quotes support "
            + "nothing at all, return a thesis citing the evidence you did read and an empty `tracks` array. That is a valid, useful answer.");
        builder.AppendLine();
        builder.AppendLine("Worked chain example — abbreviated binder evidence and the expected output shape");
        builder.AppendLine(WorkedExample);
        return builder.ToString();
    }

    /// <summary>
    ///     Appends one line per relationship kind with its meaning, where A is the `from` participant and B the `to`.
    /// </summary>
    /// <param name="builder">The prompt builder to append to.</param>
    /// <param name="relationshipKinds">The closed relationship kinds, in prompt order.</param>
    internal static void AppendRelationshipKinds(StringBuilder builder, IReadOnlyList<string> relationshipKinds)
    {
        foreach (var kind in relationshipKinds)
        {
            var meaning = kind switch
            {
                "invokes" => "A calls, dispatches to, or hands control to B.",
                "returns" => "A hands a value or result back to B.",
                "reads" => "A reads state, configuration, or data that B owns.",
                "writes" => "A creates, updates, or deletes state that B owns.",
                "publishes" => "A emits an event, message, or signal that others may receive.",
                "subscribes" => "A registers to receive an event, message, or signal emitted elsewhere.",
                "configures" => "A supplies the settings, registration, or wiring that decides how B behaves.",
                "supersedes" => "B takes over A's responsibility: moved, renamed, or reimplemented.",
                "covers" => "A is a test or check that exercises B.",
                "documents" => "A is prose, comment, or specification text describing B.",
                "contains" => "A is the file, type, or module that lexically holds B.",
                "depends-on" => "A needs B to build or run, and no more specific kind is shown by the quotes.",
                _ => "A relates to B as shown by the quoted evidence.",
            };
            builder.Append("  ").Append(kind.PadRight(12)).Append("  ").AppendLine(meaning);
        }
    }

    private static void AppendTrackShapes(StringBuilder builder, IReadOnlyList<string> trackShapes)
    {
        foreach (var shape in trackShapes)
        {
            var meaning = shape switch
            {
                "Walk" => "An ordered path through the change. Choose it when orderedSteps carries the explanation.",
                "ParticipantMap" => "A set of parts and how they relate. Choose it when relationships carries the explanation.",
                "PurposeCards" => "Independent reasons the change exists. Choose it when purposes carries the explanation.",
                _ => "A track shape whose carrying field explains the change.",
            };
            builder.Append("  ").Append(shape.PadRight(14)).Append("  ").AppendLine(meaning);
        }
    }
}
