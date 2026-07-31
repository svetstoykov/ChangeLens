using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeLens.Infrastructure.LocalState.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_runs",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    repository_id = table.Column<string>(type: "TEXT", nullable: false),
                    repository_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_repository_path = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_repository_path_key = table.Column<string>(type: "TEXT", nullable: false),
                    head_revision = table.Column<string>(type: "TEXT", nullable: false),
                    target = table.Column<string>(type: "TEXT", nullable: false),
                    target_revision = table.Column<string>(type: "TEXT", nullable: false),
                    freshness_token = table.Column<string>(type: "TEXT", nullable: false),
                    change_context = table.Column<string>(type: "TEXT", nullable: true),
                    build_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    tests_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    processor_session_id = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    requested_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    capture_started_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    captured_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    analysis_started_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    terminal_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    interrupted_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    cancellation_requested_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    snapshot_id = table.Column<string>(type: "TEXT", nullable: true),
                    terminal_limitation_count = table.Column<int>(type: "INTEGER", nullable: true),
                    terminal_failure_code = table.Column<string>(type: "TEXT", nullable: true),
                    interruption_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.run_id);
                    table.CheckConstraint("CK_analysis_runs_interruption_fields", "(state = 'interrupted' AND interrupted_at_unix_ms IS NOT NULL) OR (state <> 'interrupted' AND interrupted_at_unix_ms IS NULL)");
                    table.CheckConstraint("CK_analysis_runs_state", "state IN ('pendingCapture','capturing','discovering','collecting','checking','persisting','completed','completedWithLimitations','cancelled','failed','interrupted')");
                    table.CheckConstraint("CK_analysis_runs_terminal_fields", "(state NOT IN ('completed','completedWithLimitations','cancelled','failed') AND terminal_at_unix_ms IS NULL) OR (state IN ('completed','completedWithLimitations','cancelled','failed') AND terminal_at_unix_ms IS NOT NULL)");
                    table.CheckConstraint("CK_analysis_runs_tests_require_build", "tests_enabled = 0 OR build_enabled = 1");
                });

            migrationBuilder.CreateTable(
                name: "analysis_run_steps",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    step_id = table.Column<string>(type: "TEXT", nullable: false),
                    producer = table.Column<string>(type: "TEXT", nullable: false),
                    capability = table.Column<string>(type: "TEXT", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    stage = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    finished_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_run_steps", x => new { x.run_id, x.step_id });
                    table.CheckConstraint("CK_analysis_run_steps_state", "state IN ('pending','running','succeeded','succeededWithLimitations','failed','skipped','cancelled','timedOut')");
                    table.ForeignKey(
                        name: "FK_analysis_run_steps_analysis_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_run_steps_run_id_step_order",
                table: "analysis_run_steps",
                columns: new[] { "run_id", "step_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_active_repository",
                table: "analysis_runs",
                column: "canonical_repository_path_key",
                unique: true,
                filter: "state IN ('pendingCapture','capturing','discovering','collecting','checking','persisting')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_run_steps");

            migrationBuilder.DropTable(
                name: "analysis_runs");
        }
    }
}
