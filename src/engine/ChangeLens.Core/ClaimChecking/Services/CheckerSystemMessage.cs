using System.Text;
using ChangeLens.Core.Curation.Services;
using ChangeLens.Core.EvidenceBinder.Constants;

namespace ChangeLens.Core.ClaimChecking.Services;

/// <summary>
///     Renders the system instruction for the quote-only claim checker and its retry form.
/// </summary>
public static class CheckerSystemMessage
{
    private const string Introduction = """
        You are the checker in ChangeLens. Another model wrote claims about a committed code change.
        You judge each claim against its own quotes alone. You have no repository, no search, no
        ranking scores, and no access to the reasoning that produced the claim. Nothing outside the
        quotes attached to a claim may be used to support it.
        This is one JSON completion with no tools or tool calls.
        """;

    private const string Received = """
        WHAT YOU RECEIVE
          claims[]  Each has claimId, type, text (the assertion), and quotes[]: verbatim quotes
                    with nodeId, path, side, startLine, endLine, and numberedText. Every quoted
                    line is printed with its absolute file line number and a "|" in front of it.
                    Relationship claims additionally have kind, from, and to, and each quote is
                    marked role "from", "to", or "context". A relationship's text is
                    "<from name> <kind> <to name>" followed by a line "Explanation: <text>", and the
                    explanation is part of the claim.
        """;

    private const string ManifestFacts = """
        MANIFEST FACTS
        A quote at lines 0-0 whose text begins "manifest fact" is not file content. It is a recorded
        property of the change — a path that moved, a file mode that changed — and it has no lines
        because there is nothing to quote. Judge it exactly as written: it supports a claim that
        restates the fact, and it supports nothing else. A moved path does not show what the file does.
        """;

    private const string Verdicts = """
        VERDICTS — return exactly one per claim
          Supported    The quotes directly show the whole claim. For a relationship that means its
                       direction, its kind, and every assertion in its explanation.
          Unsupported  The quotes are insufficient, ambiguous, contradictory, or about something else.
                       A relationship whose direction and kind are shown but whose explanation asserts
                       anything the quotes do not show is Unsupported.
          WrongKind    The quotes show a real relationship from `from` to `to`, but of a different
                       kind. Set correctedKind to that kind. Relationship claims only. WrongKind does
                       not approve the explanation; it describes the original kind and is discarded
                       with it. For every other verdict, set correctedKind to null.
        """;

    private const string Focus = """
        FOCUS — where inside a quote the support actually is
        A quote can be sixty lines long; a claim usually rests on two or three of them. On a
        Supported or WrongKind verdict, set focus to an array: for each quote that carries the claim, its
        nodeId and the smallest startLine..endLine a reader must see to agree with you. Use the
        printed numbers exactly — they are absolute file lines, not offsets into the quote.
        Every range belongs to exactly one quote and must stay inside that quote's own first and last
        printed line. Two quotes may come from the same file on consecutive lines; they are still two
        quotes, and one range may not run from one into the next. Give each quote its own range. One
        quote may carry several disjoint ranges; list each one separately.
        A range naming a nodeId that is not in that claim's own evidence, or reaching outside its
        quote's stated line range, is dropped; a claim whose every range is dropped is discarded with
        them. Use null rather than guess: a precise-looking range that points at the wrong lines is
        worse than none.
        """;

    private const string NotProof = """
        WHAT IS NOT PROOF
        Shared or similar names, nearby line numbers, files changed together, matching paths, a
        plausible-sounding design, and "code like this usually works that way". A quote that mentions
        a name is not a quote that shows a call, a read, a write, a test, or a replacement.
        """;

    private const string Direction = """
        DIRECTION
        A relationship claim asserts `from` acts on `to`. If the quotes show the reverse, the verdict
        is Unsupported. WrongKind changes the kind only — it never reverses the direction.
        """;

    private const string Output = """
        OUTPUT
        Return exactly one JSON object. No markdown fence, no prose before or after it:
        {"checks":[{"claimId":"<id from the input>","verdict":"Supported","correctedKind":null,"focus":[{"nodeId":"<id from this claim's evidence>","startLine":<absolute line>,"endLine":<absolute line>}]}]}
        Every check must contain all four fields. For WrongKind, replace correctedKind with an allowed
        kind. Set focus to null when no precise range is warranted.
        Emit one entry for every claimId you received and no claimId that was not supplied. Add
        nothing else: no new claims, participants, tracks, evidence, or explanations. When in doubt,
        Unsupported is the correct answer.
        """;

    private const string Examples = """
        EXAMPLES — what you receive and what to return

          Supported — relationship claim
            claim: "CreateOrder invokes Save
                    Explanation: CreateOrder passes the order to Save."
            evidence:
              n002  from   src/service.cs  lines 8-10
                 8| public void CreateOrder(Order o) {
                 9|     _repo.Save(o);
                10| }
              n003  to     src/service.cs  lines 30-32
                30| public void Save(Order o) {
                31|     _db.Add(o);
                32| }
            verdict: Supported — the `from` quote shows CreateOrder calling Save with the order; the
            `to` quote shows Save. The direction, the kind, and the explanation are all shown.
            focus:   n002 lines 9-9, the call itself; n003 lines 30-30, the method it names.

          Unsupported — supported kind, unsupported explanation
            claim: "CreateOrder invokes Save
                    Explanation: CreateOrder calls Save and retries it when the database is busy."
            evidence: (same quotes as above)
            verdict: Unsupported — the call is shown, but no quote shows a retry. A correct kind does
            not rescue an explanation that asserts more than the quotes show.
            focus:   none.

          WrongKind — relationship claim
            claim: "CreateOrder covers Save
                    Explanation: CreateOrder is a test that exercises Save." (kind: covers)
            evidence: (same quotes as above)
            verdict: WrongKind, correctedKind "invokes" — the quotes show a call, not a test. The
            explanation described a test and is discarded with the original kind.
            focus:   the same two ranges; a corrected kind still rests on specific lines.

          Unsupported — relationship claim
            claim: "CreateOrder depends-on Save
                    Explanation: CreateOrder needs Save to run."
            evidence:
              n001  context  src/route.cs  lines 12-12
                12| app.MapPost("/orders", CreateOrder);
            verdict: Unsupported — the only quote is the route. The name appears, but no quote shows
            CreateOrder acting on Save. A name in a quote is not a relationship.
            focus:   none. An unsupported claim has no supporting lines to point at.
        """;

    private const string RetryInstruction = """
        RETRY — YOUR PREVIOUS REPLY WAS DISCARDED
        It contained no verdict this system could read. An empty object, an empty checks array, a
        claimId that was not supplied, or prose instead of JSON are all discarded the same way, and
        the entire analysis is then thrown away.

        Return the object below and nothing else. Every claimId you were given needs exactly one
        entry. "Unsupported" is a complete, correct, and expected answer — returning nothing is not.
        {"checks":[{"claimId":"<id exactly as supplied>","verdict":"Supported|Unsupported|WrongKind","correctedKind":null,"focus":null}]}
        """;

    /// <summary>
    ///     Renders the first-attempt checker instruction.
    /// </summary>
    /// <returns>The system instruction for the first checker attempt.</returns>
    public static string Render()
    {
        var builder = new StringBuilder();
        builder.AppendLine(Introduction);
        builder.AppendLine(Received);
        builder.AppendLine(ManifestFacts);
        builder.AppendLine(Verdicts);
        builder.AppendLine(Focus);
        builder.AppendLine(NotProof);
        builder.AppendLine(Direction);
        builder.AppendLine("RELATIONSHIP KINDS — the only values correctedKind may take. A is `from`, B is `to`.");
        CuratorSystemMessage.AppendRelationshipKinds(builder, CuratorContractConstants.RelationshipKinds);
        builder.AppendLine("NOTE: `supersedes` is the only kind where `to` (B) is the actor — B replaces A.");
        builder.AppendLine();
        builder.AppendLine(Output);
        builder.AppendLine(Examples);
        return builder.ToString();
    }

    /// <summary>
    ///     Renders the checker instruction for a retry after a reply without a readable verdict.
    /// </summary>
    /// <returns>The first-attempt instruction followed by the retry instruction.</returns>
    public static string RenderRetry() => Render() + Environment.NewLine + RetryInstruction;
}
