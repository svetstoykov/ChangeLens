using System.Text;

namespace ChangeLens.Core.Analysis.Shared.Helpers;

/// <summary>
///     Appends relationship-kind guidance shared by curator and claim-checker prompts.
/// </summary>
internal static class RelationshipKindGuidance
{
    /// <summary>
    ///     Appends one line per relationship kind with its meaning, where A is the `from` participant and B the `to`.
    /// </summary>
    /// <param name="builder">The prompt builder to append to. Cannot be <see langword="null" />.</param>
    /// <param name="relationshipKinds">The closed relationship kinds, in prompt order. Cannot be <see langword="null" />.</param>
    internal static void Append(StringBuilder builder, IReadOnlyList<string> relationshipKinds)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(relationshipKinds);

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
}
