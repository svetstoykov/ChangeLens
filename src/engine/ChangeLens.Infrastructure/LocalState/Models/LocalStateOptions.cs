namespace ChangeLens.Infrastructure.LocalState.Models;

/// <summary>
///     Represents the configurable settings for the local-state SQLite database.
/// </summary>
public sealed class LocalStateOptions
{
    /// <summary>
    ///     Gets or sets the configured local-state directory, or <see langword="null" /> to use local application data.
    /// </summary>
    public string? Directory { get; set; }
}
