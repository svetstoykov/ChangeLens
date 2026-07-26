namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

internal sealed class ApplicationLocalState
{
    public int SingletonId { get; set; }

    public Guid? LastRepositoryId { get; set; }

    public RepositoryLocalState? LastRepository { get; set; }

    public string? ColorTheme { get; set; }
}
