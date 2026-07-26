using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChangeLens.Infrastructure.LocalState.Persistence;

/// <summary>
///     Creates the local-state context for Entity Framework migration tooling.
/// </summary>
public sealed class ChangeLensLocalStateDbContextFactory : IDesignTimeDbContextFactory<ChangeLensLocalStateDbContext>
{
    /// <inheritdoc />
    public ChangeLensLocalStateDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>()
            .UseSqlite("Data Source=changelens.design.db")
            .Options;
        return new ChangeLensLocalStateDbContext(options);
    }
}
