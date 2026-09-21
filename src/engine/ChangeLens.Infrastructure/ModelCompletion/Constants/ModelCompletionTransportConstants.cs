namespace ChangeLens.Infrastructure.ModelCompletion.Constants;

/// <summary>
///     Defines the HTTP success-body budget for one OpenAI-compatible completion response.
/// </summary>
/// <remarks>
///     The budget covers UTF-8, JSON string escaping, and the chat-completion envelope around message content.
///     Multiplying the character cap by a UTF-8 factor alone is not enough.
/// </remarks>
public static class ModelCompletionTransportConstants
{
    /// <summary>
    ///     The worst-case JSON-string expansion per output character, covering Unicode escapes.
    /// </summary>
    public const int JsonStringExpansionFactor = 6;

    /// <summary>
    ///     Extra bytes reserved for the chat-completion envelope around the message content.
    /// </summary>
    public const int EnvelopeHeadroomBytes = 64 * 1024;

    /// <summary>
    ///     The default completion character budget used when no curator configuration is supplied.
    /// </summary>
    public const int DefaultMaximumOutputCharacters = 176_000;

    /// <summary>
    ///     Computes the HTTP success-body byte budget for a completion character cap.
    /// </summary>
    /// <param name="maximumOutputCharacters">The maximum accepted completion characters. Must be positive.</param>
    /// <returns>The byte budget, including JSON escaping and envelope headroom.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="maximumOutputCharacters" /> is zero or negative.
    /// </exception>
    public static int ResponseByteBudget(int maximumOutputCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputCharacters);
        return checked((maximumOutputCharacters * JsonStringExpansionFactor) + EnvelopeHeadroomBytes);
    }
}
