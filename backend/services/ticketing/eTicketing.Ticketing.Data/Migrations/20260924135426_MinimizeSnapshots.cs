using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class MinimizeSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "TicketingMode",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OrganizationSnapshots");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "OrganizationSnapshots");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OrganizationSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProductSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ProductSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "TicketingMode",
                table: "ProductSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProductSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OrganizationSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "OrganizationSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrganizationSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
