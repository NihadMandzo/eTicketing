using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace eTicketing.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedEventsAndBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconContentType",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IconData",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "IconBlobName",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "IconBlobName",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "IconBlobName",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "IconBlobName",
                value: null);

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "Date", "Description", "ImageUrl", "Name", "OrganizationId", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000001"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 15, 20, 0, 0, 0, DateTimeKind.Utc), "Trodnevni festival na otvorenom sa regionalnim izvođačima.", null, "Ljetni Muzički Festival", new Guid("a1a1a1a1-0000-0000-0000-000000000001"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000002"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 5, 19, 0, 0, 0, DateTimeKind.Utc), "Intimni akustični koncert u Vijećnici.", null, "Akustična Večer u Vijećnici", new Guid("a1a1a1a1-0000-0000-0000-000000000001"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000003"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 20, 18, 0, 0, 0, DateTimeKind.Utc), "Regionalni košarkaški turnir za klupske ekipe.", null, "Košarkaški Kup Mostar", new Guid("a1a1a1a1-0000-0000-0000-000000000002"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000004"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 10, 9, 0, 0, 0, DateTimeKind.Utc), "Gradski maraton kroz historijsku jezgru Mostara.", null, "Maraton Mostar", new Guid("a1a1a1a1-0000-0000-0000-000000000002"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000005"), 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 2, 9, 0, 0, 0, DateTimeKind.Utc), "Konferencija o softverskom razvoju i startupima.", null, "Tech Konferencija Sarajevo", new Guid("a1a1a1a1-0000-0000-0000-000000000001"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3c3c3c3-0000-0000-0000-000000000006"), 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 20, 18, 0, 0, 0, DateTimeKind.Utc), "Neformalno druženje lokalne startup zajednice.", null, "Startup Meetup Mostar", new Guid("a1a1a1a1-0000-0000-0000-000000000002"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Events",
                keyColumn: "Id",
                keyValue: new Guid("c3c3c3c3-0000-0000-0000-000000000006"));

            migrationBuilder.DropColumn(
                name: "IconBlobName",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "IconContentType",
                table: "Categories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "IconData",
                table: "Categories",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IconContentType", "IconData" },
                values: new object[] { "image/png", new byte[0] });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IconContentType", "IconData" },
                values: new object[] { "image/png", new byte[0] });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IconContentType", "IconData" },
                values: new object[] { "image/png", new byte[0] });
        }
    }
}
