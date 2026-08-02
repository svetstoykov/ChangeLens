using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeLens.Infrastructure.LocalState.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "captured_changed_file_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excluded_conflicted_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excluded_staged_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excluded_uncommitted_total",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excluded_unstaged_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excluded_untracked_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "head_revision_at_capture",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manifest_hash",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merge_base_revision",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_revision_at_capture",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "snapshot_manifest_entries",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    original_path = table.Column<string>(type: "TEXT", nullable: true),
                    category = table.Column<string>(type: "TEXT", nullable: false),
                    merge_base_entry_mode = table.Column<string>(type: "TEXT", nullable: false),
                    head_entry_mode = table.Column<string>(type: "TEXT", nullable: false),
                    merge_base_object_id = table.Column<string>(type: "TEXT", nullable: false),
                    head_object_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_manifest_entries", x => new { x.run_id, x.path });
                    table.CheckConstraint("CK_snapshot_manifest_entries_category", "category IN ('added','modified','deleted','renamed','typeChanged')");
                    table.CheckConstraint("CK_snapshot_manifest_entries_modes", "merge_base_entry_mode IN ('000000','100644','100755','120000','160000') AND head_entry_mode IN ('000000','100644','100755','120000','160000')");
                    table.CheckConstraint("CK_snapshot_manifest_entries_object_ids", "length(merge_base_object_id) = length(head_object_id) AND length(head_object_id) IN (40, 64) AND merge_base_object_id NOT GLOB '*[^0-9a-f]*' AND head_object_id NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("CK_snapshot_manifest_entries_original_path", "(category = 'renamed' AND original_path IS NOT NULL) OR (category <> 'renamed' AND original_path IS NULL)");
                    table.ForeignKey(
                        name: "FK_snapshot_manifest_entries_analysis_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_capture_counts",
                table: "analysis_runs",
                sql: "(captured_changed_file_count IS NULL OR captured_changed_file_count >= 0) AND (excluded_uncommitted_total IS NULL OR excluded_uncommitted_total >= 0) AND (excluded_staged_count IS NULL OR excluded_staged_count >= 0) AND (excluded_unstaged_count IS NULL OR excluded_unstaged_count >= 0) AND (excluded_untracked_count IS NULL OR excluded_untracked_count >= 0) AND (excluded_conflicted_count IS NULL OR excluded_conflicted_count >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_capture_fields",
                table: "analysis_runs",
                sql: "(captured_at_unix_ms IS NULL AND snapshot_id IS NULL AND manifest_hash IS NULL AND target_revision_at_capture IS NULL AND head_revision_at_capture IS NULL AND merge_base_revision IS NULL AND captured_changed_file_count IS NULL AND excluded_uncommitted_total IS NULL AND excluded_staged_count IS NULL AND excluded_unstaged_count IS NULL AND excluded_untracked_count IS NULL AND excluded_conflicted_count IS NULL) OR (captured_at_unix_ms IS NOT NULL AND snapshot_id IS NOT NULL AND manifest_hash IS NOT NULL AND target_revision_at_capture IS NOT NULL AND head_revision_at_capture IS NOT NULL AND merge_base_revision IS NOT NULL AND captured_changed_file_count IS NOT NULL AND excluded_uncommitted_total IS NOT NULL AND excluded_staged_count IS NOT NULL AND excluded_unstaged_count IS NOT NULL AND excluded_untracked_count IS NOT NULL AND excluded_conflicted_count IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_manifest_hash",
                table: "analysis_runs",
                sql: "manifest_hash IS NULL OR (length(manifest_hash) = 64 AND manifest_hash NOT GLOB '*[^0-9a-f]*')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshot_manifest_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_capture_counts",
                table: "analysis_runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_capture_fields",
                table: "analysis_runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_manifest_hash",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "captured_changed_file_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "excluded_conflicted_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "excluded_staged_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "excluded_uncommitted_total",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "excluded_unstaged_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "excluded_untracked_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "head_revision_at_capture",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "manifest_hash",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "merge_base_revision",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "target_revision_at_capture",
                table: "analysis_runs");
        }
    }
}
