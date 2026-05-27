using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsrsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixAccountUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedAccounts_OsrsUsername",
                table: "TrackedAccounts");

            migrationBuilder.DropIndex(
                name: "IX_TrackedAccounts_UserId",
                table: "TrackedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedAccounts_UserId_OsrsUsername",
                table: "TrackedAccounts",
                columns: new[] { "UserId", "OsrsUsername" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedAccounts_UserId_OsrsUsername",
                table: "TrackedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedAccounts_OsrsUsername",
                table: "TrackedAccounts",
                column: "OsrsUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedAccounts_UserId",
                table: "TrackedAccounts",
                column: "UserId");
        }
    }
}
