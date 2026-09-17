using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_CreatedAt",
                table: "Products",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Status_CreatedAt",
                table: "Products");
        }
    }
}
