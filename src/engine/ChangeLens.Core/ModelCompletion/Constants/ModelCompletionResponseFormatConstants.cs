namespace ChangeLens.Core.ModelCompletion.Constants;

/// <summary>
///     Provides protocol literals for the JSON-object response format requested from model completion providers.
/// </summary>
public static class ModelCompletionResponseFormatConstants
{
    /// <summary>The JSON property name identifying the response format type.</summary>
    public const string TypePropertyName = "type";

    /// <summary>The response format type value requesting a JSON object reply.</summary>
    public const string JsonObjectType = "json_object";
}
