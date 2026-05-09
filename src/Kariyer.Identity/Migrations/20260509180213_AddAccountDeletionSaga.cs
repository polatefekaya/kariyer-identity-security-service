using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_deletion_saga_state",
                schema: "identity",
                columns: table => new
                {
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state = table.Column<string>(type: "text", nullable: false),
                    user_uid = table.Column<string>(type: "text", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiated_by = table.Column<string>(type: "text", nullable: false),
                    initiated_by_uid = table.Column<string>(type: "text", nullable: true),
                    grace_period_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supabase_banned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_uid = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_saga_state", x => x.correlation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_saga_state_grace_period",
                schema: "identity",
                table: "account_deletion_saga_state",
                columns: new[] { "current_state", "grace_period_ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_saga_state_user_uid",
                schema: "identity",
                table: "account_deletion_saga_state",
                column: "user_uid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_deletion_saga_state",
                schema: "identity");
        }
    }
}
