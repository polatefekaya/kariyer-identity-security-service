using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionSagaActiveUserConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_account_deletion_saga_state_user_uid",
                schema: "identity",
                table: "account_deletion_saga_state");

            migrationBuilder.CreateIndex(
                name: "uix_account_deletion_saga_state_active_user",
                schema: "identity",
                table: "account_deletion_saga_state",
                column: "user_uid",
                unique: true,
                filter: "current_state NOT IN ('Deleted', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uix_account_deletion_saga_state_active_user",
                schema: "identity",
                table: "account_deletion_saga_state");

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_saga_state_user_uid",
                schema: "identity",
                table: "account_deletion_saga_state",
                column: "user_uid");
        }
    }
}
