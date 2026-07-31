using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.AnalysisRuns.Services;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns.Support;

/// <summary>
///     Prepares an isolated, migrated local-state SQLite database with one seeded repository and a ready
///     <see cref="SqliteAnalysisRunStore" /> for analysis-run store integration tests.
/// </summary>
internal sealed class AnalysisRunStoreTestFixture : IAsyncDisposable
{
    private const string DefaultCanonicalPath = "/repository";

    private readonly TemporaryDirectory _temporaryDirectory;

    private AnalysisRunStoreTestFixture(TemporaryDirectory temporaryDirectory, ChangeLensLocalStateDbContext context)
    {
        this._temporaryDirectory = temporaryDirectory;
        this.Context = context;
        this.ProcessorSessionId = Guid.NewGuid();
        this.Store = new SqliteAnalysisRunStore(context, TimeProvider.System, NullLogger<SqliteAnalysisRunStore>.Instance);
    }

    /// <summary>
    ///     Gets the store under test.
    /// </summary>
    public SqliteAnalysisRunStore Store { get; }

    /// <summary>
    ///     Gets the processor-session identifier used by <see cref="Acceptance" /> unless overridden.
    /// </summary>
    public Guid ProcessorSessionId { get; }

    private ChangeLensLocalStateDbContext Context { get; }

    /// <summary>
    ///     Asynchronously creates a migrated fixture with one repository seeded at <see cref="DefaultCanonicalPath" />.
    /// </summary>
    /// <returns>A task whose result contains the ready fixture.</returns>
    public static async Task<AnalysisRunStoreTestFixture> CreateAsync()
    {
        var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        var context = CreateContext(paths);
        var initializer = new SqliteLocalStateInitializer(context, paths, NullLogger<SqliteLocalStateInitializer>.Instance);
        var initializeResult = await initializer.InitializeAsync(CancellationToken.None);
        if (!initializeResult.IsSuccess)
        {
            throw new InvalidOperationException("Failed to initialize the local-state database for a test fixture.");
        }

        var fixture = new AnalysisRunStoreTestFixture(temporaryDirectory, context);
        await fixture.SeedRepositoryAsync(DefaultCanonicalPath);
        return fixture;
    }

    /// <summary>
    ///     Asynchronously seeds one repository row at the given canonical path and returns its identifier.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository path. Cannot be <see langword="null" />.</param>
    /// <returns>A task whose result contains the seeded repository identifier.</returns>
    public async Task<Guid> SeedRepositoryAsync(string canonicalPath)
    {
        var repository = new RepositoryLocalState
        {
            RepositoryId = Guid.NewGuid(),
            CanonicalPath = canonicalPath,
            CanonicalPathKey = canonicalPath,
            DisplayName = "change_lens",
            LastOpenedAtUnixMilliseconds = 1_000,
        };
        this.Context.Repositories.Add(repository);
        await this.Context.SaveChangesAsync(CancellationToken.None);
        return repository.RepositoryId;
    }

    /// <summary>
    ///     Builds a valid <see cref="AnalysisRunAcceptance" /> for the seeded default repository, unless
    ///     <paramref name="canonicalPath" /> selects a different seeded repository.
    /// </summary>
    /// <param name="canonicalPath">
    ///     The canonical path of the seeded repository, or <see langword="null" /> to use the default repository.
    /// </param>
    /// <param name="processorSessionId">
    ///     The accepting processor-session identifier, or <see langword="null" /> to use <see cref="ProcessorSessionId" />.
    /// </param>
    /// <param name="requestedAtUnixMilliseconds">The acceptance timestamp.</param>
    /// <returns>A valid acceptance request.</returns>
    public AnalysisRunAcceptance Acceptance(
        string? canonicalPath = null,
        Guid? processorSessionId = null,
        long requestedAtUnixMilliseconds = 1_000) =>
        new(
            canonicalPath ?? DefaultCanonicalPath,
            "change_lens",
            canonicalPath ?? DefaultCanonicalPath,
            "head-revision",
            "main",
            "target-revision",
            "freshness-token",
            new AnalysisCheckSelection(true, true),
            null,
            processorSessionId ?? this.ProcessorSessionId,
            requestedAtUnixMilliseconds);

    /// <summary>
    ///     Asynchronously reads one persisted run entity directly, for row-level assertions.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <returns>A task whose result contains the persisted run entity.</returns>
    public async Task<AnalysisRunEntity> GetRunAsync(Guid runId) =>
        await this.Context.AnalysisRuns.AsNoTracking().SingleAsync(run => run.RunId == runId, CancellationToken.None);

    /// <summary>
    ///     Asynchronously reads one persisted run step entity directly, for row-level assertions.
    /// </summary>
    /// <param name="runId">The owning run identifier.</param>
    /// <param name="stepId">The stable step identifier. Cannot be <see langword="null" />.</param>
    /// <returns>A task whose result contains the persisted step entity.</returns>
    public async Task<AnalysisRunStepEntity> GetStepAsync(Guid runId, string stepId) =>
        await this.Context.AnalysisRunSteps
            .AsNoTracking()
            .SingleAsync(step => step.RunId == runId && step.StepId == stepId, CancellationToken.None);

    /// <summary>
    ///     Asynchronously accepts a run for the seeded default repository and returns its identifier.
    /// </summary>
    /// <returns>A task whose result contains the accepted run identifier.</returns>
    public async Task<Guid> CreateAcceptedRunAsync()
    {
        var outcome = await this.Store.CreateOrReturnActiveAsync(this.Acceptance(), CancellationToken.None);
        return outcome.Data!.RunId!.Value;
    }

    /// <summary>
    ///     Asynchronously disposes the local-state context and deletes the temporary directory.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await this.Context.DisposeAsync();
        this._temporaryDirectory.Dispose();
    }

    private static ChangeLensLocalStateDbContext CreateContext(LocalStatePaths paths)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = 5,
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>().UseSqlite(connectionString).Options;
        return new ChangeLensLocalStateDbContext(options);
    }
}
