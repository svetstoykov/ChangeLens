using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the parameters for listing comparison targets.
/// </summary>
internal sealed class ComparisonListTargetsParameters
{
    private string? _query;
    private string? _after;
    private string? _targetSetToken;

    /// <summary>
    ///     Gets the selected repository directory path.
    /// </summary>
    [JsonRequired]
    public string Path { get; init; } = null!;

    /// <summary>
    ///     Gets the exact optional target-name query.
    /// </summary>
    /// <exception cref="JsonException">
    ///     The property is explicitly set to <see langword="null" />.
    /// </exception>
    public string? Query
    {
        get => _query;
        init => _query = value ?? throw new JsonException(
            "An explicitly supplied target query cannot be null.");
    }

    /// <summary>
    ///     Gets the exact optional full-ref continuation cursor.
    /// </summary>
    /// <exception cref="JsonException">
    ///     The property is explicitly set to <see langword="null" />.
    /// </exception>
    public string? After
    {
        get => _after;
        init => _after = value ?? throw new JsonException(
            "An explicitly supplied target cursor cannot be null.");
    }

    /// <summary>
    ///     Gets the optional target-set token paired with the continuation cursor.
    /// </summary>
    /// <exception cref="JsonException">
    ///     The property is explicitly set to <see langword="null" />.
    /// </exception>
    public string? TargetSetToken
    {
        get => _targetSetToken;
        init => _targetSetToken = value ?? throw new JsonException(
            "An explicitly supplied target-set token cannot be null.");
    }
}
