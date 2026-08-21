using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Payment.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeOrderRefUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderRef",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderRef",
                table: "Payments",
                column: "OrderRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderRef",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderRef",
                table: "Payments",
                column: "OrderRef");
        }
    }
}
