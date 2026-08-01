using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Converters;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChangeLens.Infrastructure.LocalState.Persistence;

/// <summary>
///     Represents one scoped Entity Framework session over ChangeLens local state.
/// </summary>
/// <remarks>
///     The Engine registers this context as scoped. It serves one boot or request scope and does not need to be
///     thread-safe.
/// </remarks>
/// <param name="options">The scoped local-state context options. Cannot be <see langword="null" />.</param>
public sealed class ChangeLensLocalStateDbContext(
    DbContextOptions<ChangeLensLocalStateDbContext> options) : DbContext(options)
{
    internal DbSet<LocalStateMetadata> Metadata => this.Set<LocalStateMetadata>();

    internal DbSet<RepositoryLocalState> Repositories => this.Set<RepositoryLocalState>();

    internal DbSet<ApplicationLocalState> ApplicationState => this.Set<ApplicationLocalState>();

    internal DbSet<AnalysisRunEntity> AnalysisRuns => this.Set<AnalysisRunEntity>();

    internal DbSet<AnalysisRunStepEntity> AnalysisRunSteps => this.Set<AnalysisRunStepEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalStateMetadata>(entity =>
        {
            entity.ToTable("local_state_metadata", table =>
            {
                table.HasCheckConstraint("CK_local_state_metadata_singleton", "singleton_id = 1");
                table.HasCheckConstraint("CK_local_state_metadata_product", "product_name = 'ChangeLens'");
                table.HasCheckConstraint("CK_local_state_metadata_schema", "schema_version > 0");
            });
            entity.HasKey(metadata => metadata.SingletonId);
            entity.Property(metadata => metadata.SingletonId).HasColumnName("singleton_id").ValueGeneratedNever();
            entity.Property(metadata => metadata.ProductName).HasColumnName("product_name").IsRequired();
            entity.Property(metadata => metadata.SchemaVersion).HasColumnName("schema_version");
            entity.Property(metadata => metadata.CreatedAtUnixMilliseconds).HasColumnName("created_at_unix_ms");
        });

        modelBuilder.Entity<RepositoryLocalState>(entity =>
        {
            entity.ToTable("repositories");
            entity.HasKey(repository => repository.RepositoryId);
            entity.Property(repository => repository.RepositoryId).HasColumnName("repository_id").HasConversion<string>();
            entity.Property(repository => repository.CanonicalPath).HasColumnName("canonical_path").IsRequired();
            entity.Property(repository => repository.CanonicalPathKey).HasColumnName("canonical_path_key").IsRequired();
            entity.HasIndex(repository => repository.CanonicalPathKey).IsUnique();
            entity.Property(repository => repository.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(repository => repository.LastOpenedAtUnixMilliseconds).HasColumnName("last_opened_at_unix_ms");
            entity.Property(repository => repository.PreferredTargetFullName).HasColumnName("preferred_target_full_name");
        });

        modelBuilder.Entity<ApplicationLocalState>(entity =>
        {
            entity.ToTable("application_state", table =>
            {
                table.HasCheckConstraint("CK_application_state_singleton", "singleton_id = 1");
                table.HasCheckConstraint("CK_application_state_color_theme", "color_theme IS NULL OR color_theme IN ('light', 'dark')");
            });
            entity.HasKey(application => application.SingletonId);
            entity.Property(application => application.SingletonId).HasColumnName("singleton_id").ValueGeneratedNever();
            entity.Property(application => application.LastRepositoryId).HasColumnName("last_repository_id").HasConversion<string>();
            entity.Property(application => application.ColorTheme).HasColumnName("color_theme");
            entity.HasOne(application => application.LastRepository).WithMany().HasForeignKey(application => application.LastRepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AnalysisRunEntity>(entity =>
        {
            entity.ToTable("analysis_runs", table =>
            {
                table.HasCheckConstraint(
                    "CK_analysis_runs_state",
                    "state IN ('pendingCapture','capturing','discovering','collecting','persisting'," +
                    "'completed','completedWithLimitations','cancelled','failed','interrupted')");
                table.HasCheckConstraint(
                    "CK_analysis_runs_terminal_fields",
                    "(state NOT IN ('completed','completedWithLimitations','cancelled','failed') " +
                    "AND terminal_at_unix_ms IS NULL) OR " +
                    "(state IN ('completed','completedWithLimitations','cancelled','failed') " +
                    "AND terminal_at_unix_ms IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_analysis_runs_interruption_fields",
                    "(state = 'interrupted' AND interrupted_at_unix_ms IS NOT NULL) OR " +
                    "(state <> 'interrupted' AND interrupted_at_unix_ms IS NULL)");
            });
            entity.HasKey(run => run.RunId);
            entity.Property(run => run.RunId).HasColumnName("run_id").HasConversion<string>();
            entity.Property(run => run.RepositoryId).HasColumnName("repository_id").HasConversion<string>();
            entity.Property(run => run.RepositoryDisplayName).HasColumnName("repository_display_name").IsRequired();
            entity.Property(run => run.CanonicalRepositoryPath).HasColumnName("canonical_repository_path").IsRequired();
            entity.Property(run => run.CanonicalRepositoryPathKey).HasColumnName("canonical_repository_path_key").IsRequired();
            entity.Property(run => run.HeadRevision).HasColumnName("head_revision").IsRequired();
            entity.Property(run => run.Target).HasColumnName("target").IsRequired();
            entity.Property(run => run.TargetRevision).HasColumnName("target_revision").IsRequired();
            entity.Property(run => run.FreshnessToken).HasColumnName("freshness_token").IsRequired();
            entity.Property(run => run.ChangeContext).HasColumnName("change_context");
            entity.Property(run => run.State).HasColumnName("state").HasConversion<AnalysisRunStateValueConverter>();
            entity.Property(run => run.RequestedAtUnixMilliseconds).HasColumnName("requested_at_unix_ms");
            entity.Property(run => run.CaptureStartedAtUnixMilliseconds).HasColumnName("capture_started_at_unix_ms");
            entity.Property(run => run.CapturedAtUnixMilliseconds).HasColumnName("captured_at_unix_ms");
            entity.Property(run => run.AnalysisStartedAtUnixMilliseconds).HasColumnName("analysis_started_at_unix_ms");
            entity.Property(run => run.TerminalAtUnixMilliseconds).HasColumnName("terminal_at_unix_ms");
            entity.Property(run => run.InterruptedAtUnixMilliseconds).HasColumnName("interrupted_at_unix_ms");
            entity.Property(run => run.CancellationRequestedAtUnixMilliseconds).HasColumnName("cancellation_requested_at_unix_ms");
            entity.Property(run => run.SnapshotId).HasColumnName("snapshot_id");
            entity.Property(run => run.TerminalLimitationCount).HasColumnName("terminal_limitation_count");
            entity.Property(run => run.TerminalFailureCode).HasColumnName("terminal_failure_code");
            entity.Property(run => run.InterruptionReason).HasColumnName("interruption_reason");
            entity.HasIndex(run => run.CanonicalRepositoryPathKey).HasDatabaseName("IX_analysis_runs_active_repository").IsUnique()
                .HasFilter("state IN ('pendingCapture','capturing','discovering','collecting','persisting')");
        });

        modelBuilder.Entity<AnalysisRunStepEntity>(entity =>
        {
            entity.ToTable("analysis_run_steps", table =>
            {
                table.HasCheckConstraint(
                    "CK_analysis_run_steps_state",
                    "state IN ('pending','running','succeeded','succeededWithLimitations','failed'," +
                    "'skipped','cancelled','timedOut')");
            });
            entity.HasKey(step => new { step.RunId, step.StepId });
            entity.Property(step => step.RunId).HasColumnName("run_id").HasConversion<string>();
            entity.Property(step => step.StepId).HasColumnName("step_id");
            entity.Property(step => step.Producer).HasColumnName("producer").IsRequired();
            entity.Property(step => step.Capability).HasColumnName("capability").IsRequired();
            entity.Property(step => step.Order).HasColumnName("step_order");
            entity.Property(step => step.Stage).HasColumnName("stage").HasConversion<AnalysisStageValueConverter>();
            entity.Property(step => step.State).HasColumnName("state").HasConversion<AnalysisRunStepStateValueConverter>();
            entity.Property(step => step.StartedAtUnixMilliseconds).HasColumnName("started_at_unix_ms");
            entity.Property(step => step.FinishedAtUnixMilliseconds).HasColumnName("finished_at_unix_ms");
            entity.Property(step => step.Code).HasColumnName("code");
            entity.HasIndex(step => new { step.RunId, step.Order }).IsUnique();
            entity.HasOne(step => step.Run).WithMany(run => run.Steps).HasForeignKey(step => step.RunId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
