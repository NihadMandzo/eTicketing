using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace eTicketing.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameEventToProductAddTicketingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RenameTable (not DropTable+CreateTable) so any environment that already has Events
            // rows keeps them through the rename instead of losing them. PK/FK/index names are
            // renamed too so they match the convention-derived names EF's model snapshot expects
            // for the Products table going forward.
            migrationBuilder.RenameTable(
                name: "Events",
                newName: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Events",
                table: "Products");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "Id");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Categories_CategoryId",
                table: "Products");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.RenameIndex(
                name: "IX_Events_CategoryId",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Events_OrganizationId",
                table: "Products",
                newName: "IX_Products_OrganizationId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Products",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "TicketingMode",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "TicketingMode",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "TicketingMode",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "TicketingMode",
                value: 0);

            // No InsertData here — unlike the original DropTable/CreateTable version, RenameTable
            // preserves the 6 rows Events already had (seeded by the prior migration), so
            // re-inserting them under Products would just collide on PK.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketingMode",
                table: "Categories");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Products",
                type: "datetime2",
                nullable: false,
                defaultValue: default(DateTime),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.RenameIndex(
                name: "IX_Products_OrganizationId",
                table: "Products",
                newName: "IX_Events_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                newName: "IX_Events_CategoryId");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Events",
                table: "Products",
                column: "Id");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Events");
        }
    }
}
