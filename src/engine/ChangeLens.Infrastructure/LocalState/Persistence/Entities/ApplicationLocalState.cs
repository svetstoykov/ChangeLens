namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

/// <summary>
///     Represents persisted application-level local state.
/// </summary>
internal sealed class ApplicationLocalState
{
    /// <summary>
    ///     Gets or sets the singleton row identifier.
    /// </summary>
    public int SingletonId { get; set; }

    /// <summary>
    ///     Gets or sets the identifier of the last selected repository, or <see langword="null" /> when none is selected.
    /// </summary>
    public Guid? LastRepositoryId { get; set; }

    /// <summary>
    ///     Gets or sets the last selected repository, or <see langword="null" /> when none is selected.
    /// </summary>
    public RepositoryLocalState? LastRepository { get; set; }

    /// <summary>
    ///     Gets or sets the persisted color-theme preference, or <see langword="null" /> when none is configured.
    /// </summary>
    public string? ColorTheme { get; set; }
}
