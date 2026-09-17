namespace ChangeLens.Core.ContextPolicy.Constants;

/// <summary>
///     Defines configuration keys for context policy disclosure.
/// </summary>
public static class ContextPolicyConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing context policy settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:ContextPolicy";

    /// <summary>
    ///     The configuration key for the maximum disclosed characters per node.
    /// </summary>
    public const string MaximumDisclosedCharactersPerNodeKey = SectionKey + ":MaximumDisclosedCharactersPerNode";
}
