namespace ChangeLens.Infrastructure.ModelCompletion.Constants;

/// <summary>Defines the recognized completion-envelope fields the adapter reads for diagnostics.</summary>
internal static class CompletionEnvelopeConstants
{
    /// <summary>The generation identifier member.</summary>
    internal const string Id = "id";

    /// <summary>The upstream provider member an aggregator such as OpenRouter adds.</summary>
    internal const string Provider = "provider";

    /// <summary>The reasoning text member of a completion message.</summary>
    internal const string Reasoning = "reasoning";

    /// <summary>The finish reason that marks a reply cut off at the output-token limit.</summary>
    internal const string OutputLimitFinishReason = "length";

    /// <summary>The longest identifier the adapter logs; a longer value is treated as absent.</summary>
    internal const int MaximumIdentifierLength = 64;
}
