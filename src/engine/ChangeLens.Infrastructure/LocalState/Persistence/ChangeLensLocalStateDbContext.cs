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
            entity.Property(metadata => metadata.SingletonId)
                .HasColumnName("singleton_id")
                .ValueGeneratedNever();
            entity.Property(metadata => metadata.ProductName).HasColumnName("product_name").IsRequired();
            entity.Property(metadata => metadata.SchemaVersion).HasColumnName("schema_version");
            entity.Property(metadata => metadata.CreatedAtUnixMilliseconds).HasColumnName("created_at_unix_ms");
        });

        modelBuilder.Entity<RepositoryLocalState>(entity =>
        {
            entity.ToTable("repositories");
            entity.HasKey(repository => repository.RepositoryId);
            entity.Property(repository => repository.RepositoryId)
                .HasColumnName("repository_id")
                .HasConversion<string>();
            entity.Property(repository => repository.CanonicalPath).HasColumnName("canonical_path").IsRequired();
            entity.Property(repository => repository.CanonicalPathKey).HasColumnName("canonical_path_key").IsRequired();
            entity.HasIndex(repository => repository.CanonicalPathKey).IsUnique();
            entity.Property(repository => repository.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(repository => repository.LastOpenedAtUnixMilliseconds)
                .HasColumnName("last_opened_at_unix_ms");
            entity.Property(repository => repository.PreferredTargetFullName)
                .HasColumnName("preferred_target_full_name");
        });

        modelBuilder.Entity<ApplicationLocalState>(entity =>
        {
            entity.ToTable("application_state", table =>
            {
                table.HasCheckConstraint("CK_application_state_singleton", "singleton_id = 1");
                table.HasCheckConstraint(
                    "CK_application_state_color_theme",
                    "color_theme IS NULL OR color_theme IN ('light', 'dark')");
            });
            entity.HasKey(application => application.SingletonId);
            entity.Property(application => application.SingletonId)
                .HasColumnName("singleton_id")
                .ValueGeneratedNever();
            entity.Property(application => application.LastRepositoryId)
                .HasColumnName("last_repository_id")
                .HasConversion<string>();
            entity.Property(application => application.ColorTheme).HasColumnName("color_theme");
            entity.HasOne(application => application.LastRepository)
                .WithMany()
                .HasForeignKey(application => application.LastRepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
