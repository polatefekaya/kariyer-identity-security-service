using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialUpdateSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credential_update_saga_state",
                schema: "identity",
                columns: table => new
                {
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state = table.Column<string>(type: "text", nullable: false),
                    user_uid = table.Column<string>(type: "text", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_type = table.Column<string>(type: "text", nullable: false),
                    new_value = table.Column<string>(type: "text", nullable: false),
                    new_hash = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: false),
                    old_hash = table.Column<string>(type: "text", nullable: true),
                    initiated_by = table.Column<string>(type: "text", nullable: false),
                    notification_email = table.Column<string>(type: "text", nullable: false),
                    notification_full_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credential_update_saga_state", x => x.correlation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credential_update_saga_state_current_state",
                schema: "identity",
                table: "credential_update_saga_state",
                column: "current_state");

            migrationBuilder.CreateIndex(
                name: "ix_credential_update_saga_state_user_uid",
                schema: "identity",
                table: "credential_update_saga_state",
                column: "user_uid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credential_update_saga_state",
                schema: "identity");
        }
    }
}
