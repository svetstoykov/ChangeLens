namespace ChangeLens.Core.ClaimChecking.Constants;

/// <summary>
///     Defines configuration keys for claim checking and the model-backed checker.
/// </summary>
public static class ClaimCheckingConfigurationConstants
{
    /// <summary>
    ///     Gets the configuration section containing checker settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:Checker";

    /// <summary>
    ///     Gets the configuration key that enables claim checking.
    /// </summary>
    public const string EnabledKey = SectionKey + ":Enabled";

    /// <summary>
    ///     Gets the configuration key for the maximum number of claims submitted in one checker call.
    /// </summary>
    public const string MaximumClaimsKey = SectionKey + ":MaximumClaims";

    /// <summary>
    ///     Gets the configuration key for the maximum claims-payload size in characters.
    /// </summary>
    public const string MaximumPayloadCharactersKey = SectionKey + ":MaximumPayloadCharacters";

    /// <summary>
    ///     Gets the configuration key for the number of completion attempts.
    /// </summary>
    public const string AttemptsKey = SectionKey + ":Attempts";

    /// <summary>
    ///     Gets the configuration key for the optional provider reasoning effort.
    /// </summary>
    public const string ReasoningEffortKey = SectionKey + ":ReasoningEffort";

    /// <summary>
    ///     Gets the configuration key for the maximum output-token request.
    /// </summary>
    public const string MaximumOutputTokensKey = SectionKey + ":MaximumOutputTokens";
}
