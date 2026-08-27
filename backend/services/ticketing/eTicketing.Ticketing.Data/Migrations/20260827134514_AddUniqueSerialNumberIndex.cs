using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueSerialNumberIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets",
                columns: new[] { "ProductId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets",
                columns: new[] { "ProductId", "SerialNumber" });
        }
    }
}
