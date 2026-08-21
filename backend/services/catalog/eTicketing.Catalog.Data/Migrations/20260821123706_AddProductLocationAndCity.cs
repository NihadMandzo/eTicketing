using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLocationAndCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "City",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Products",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Products",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000001"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 0, 43.856299999999997, 18.4131 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000002"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 0, 43.856299999999997, 18.4131 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000003"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 1, 43.343800000000002, 17.8078 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000004"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 1, 43.343800000000002, 17.8078 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000005"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 0, 43.856299999999997, 18.4131 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000006"),
                columns: new[] { "City", "Latitude", "Longitude" },
                values: new object[] { 1, 43.343800000000002, 17.8078 });

            migrationBuilder.CreateIndex(
                name: "IX_Products_City",
                table: "Products",
                column: "City");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_City",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Products");
        }
    }
}
