using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeLens.Infrastructure.LocalState.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLocalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_state_metadata",
                columns: table => new
                {
                    singleton_id = table.Column<int>(type: "INTEGER", nullable: false),
                    product_name = table.Column<string>(type: "TEXT", nullable: false),
                    schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_state_metadata", x => x.singleton_id);
                    table.CheckConstraint("CK_local_state_metadata_product", "product_name = 'ChangeLens'");
                    table.CheckConstraint("CK_local_state_metadata_schema", "schema_version > 0");
                    table.CheckConstraint("CK_local_state_metadata_singleton", "singleton_id = 1");
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    repository_id = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_path = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_path_key = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_opened_at_unix_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    preferred_target_full_name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.repository_id);
                });

            migrationBuilder.CreateTable(
                name: "application_state",
                columns: table => new
                {
                    singleton_id = table.Column<int>(type: "INTEGER", nullable: false),
                    last_repository_id = table.Column<string>(type: "TEXT", nullable: true),
                    color_theme = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_state", x => x.singleton_id);
                    table.CheckConstraint("CK_application_state_color_theme", "color_theme IS NULL OR color_theme IN ('light', 'dark')");
                    table.CheckConstraint("CK_application_state_singleton", "singleton_id = 1");
                    table.ForeignKey(
                        name: "FK_application_state_repositories_last_repository_id",
                        column: x => x.last_repository_id,
                        principalTable: "repositories",
                        principalColumn: "repository_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_state_last_repository_id",
                table: "application_state",
                column: "last_repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_repositories_canonical_path_key",
                table: "repositories",
                column: "canonical_path_key",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO local_state_metadata
                    (singleton_id, product_name, schema_version, created_at_unix_ms)
                VALUES (1, 'ChangeLens', 1, CAST(strftime('%s', 'now') AS INTEGER) * 1000);
                INSERT INTO application_state (singleton_id, last_repository_id, color_theme)
                VALUES (1, NULL, NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_state");

            migrationBuilder.DropTable(
                name: "local_state_metadata");

            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}
