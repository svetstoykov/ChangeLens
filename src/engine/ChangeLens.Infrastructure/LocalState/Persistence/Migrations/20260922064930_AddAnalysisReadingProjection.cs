using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeLens.Infrastructure.LocalState.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisReadingProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "correspondence_candidate_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "disclosed_evidence_node_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reading_model_json",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "validation_removal_count",
                table: "analysis_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validation_removals_json",
                table: "analysis_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_discovery_counts",
                table: "analysis_runs",
                sql: "(correspondence_candidate_count IS NULL OR correspondence_candidate_count >= 0) AND (disclosed_evidence_node_count IS NULL OR disclosed_evidence_node_count >= 0) AND (validation_removal_count IS NULL OR validation_removal_count >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_reading_projection",
                table: "analysis_runs",
                sql: "(reading_model_json IS NULL AND validation_removals_json IS NULL) OR (reading_model_json IS NOT NULL AND validation_removals_json IS NOT NULL AND state IN ('completed','completedWithLimitations'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_discovery_counts",
                table: "analysis_runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_reading_projection",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "correspondence_candidate_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "disclosed_evidence_node_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "reading_model_json",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "validation_removal_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "validation_removals_json",
                table: "analysis_runs");
        }
    }
}
