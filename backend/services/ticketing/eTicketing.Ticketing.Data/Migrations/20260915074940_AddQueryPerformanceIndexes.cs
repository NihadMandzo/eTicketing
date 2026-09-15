using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedAt_Status",
                table: "Tickets",
                columns: new[] { "CreatedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UserId_CreatedAt",
                table: "Tickets",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ValidatedAt",
                table: "Tickets",
                column: "ValidatedAt",
                filter: "[ValidatedAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_ProductId_Status",
                table: "Sectors",
                columns: new[] { "ProductId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedAt_Status",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_UserId_CreatedAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ValidatedAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Sectors_ProductId_Status",
                table: "Sectors");
        }
    }
}
