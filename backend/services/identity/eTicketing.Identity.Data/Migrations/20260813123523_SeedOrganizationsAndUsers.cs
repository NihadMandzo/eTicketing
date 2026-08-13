using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace eTicketing.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedOrganizationsAndUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LogoData",
                table: "Organizations");

            migrationBuilder.AddColumn<string>(
                name: "LogoBlobName",
                table: "Organizations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "Address", "CreatedAt", "Description", "Email", "IsActive", "LogoBlobName", "Name", "PhoneNumber", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { new Guid("a1a1a1a1-0000-0000-0000-000000000001"), "Ferhadija 12, Sarajevo", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Organizator koncerata i festivala u Sarajevu.", "info@sarajevo-events.ba", true, null, "Sarajevo Events", "+387 33 123 456", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "https://sarajevo-events.ba" },
                    { new Guid("a1a1a1a1-0000-0000-0000-000000000002"), "Kralja Tomislava 5, Mostar", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Organizacija sportskih događaja u Mostaru i regiji.", "kontakt@mostar-sport.ba", true, null, "Mostar Sport Arena", "+387 36 987 654", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "IsActive", "IsEmailVerified", "IsFirstLogin", "LastLoginAt", "LastName", "OrganizationId", "PasswordHash", "PasswordSalt", "PhoneNumber", "Role", "UpdatedAt", "Username" },
                values: new object[,]
                {
                    { new Guid("b2b2b2b2-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "superadmin@eticketing.local", "Amina", true, true, false, null, "Hodžić", null, "Vo2N5mMKKV5riRxNcu+xYv2AxWVVkmJLATTjr04SU3I=", "AdVMNBExbubfvQWaHVw5Vg==", null, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "superadmin" },
                    { new Guid("b2b2b2b2-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "emir@sarajevo-events.ba", "Emir", true, true, false, null, "Kovačević", new Guid("a1a1a1a1-0000-0000-0000-000000000001"), "A6rZYgmOEHLfpoLnHEiPWOFfkAH3yQUaWv1PqvOzC18=", "ucX6FvDXRYs6cWtGnpOc9Q==", null, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "emir.kovacevic" },
                    { new Guid("b2b2b2b2-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "lejla@sarajevo-events.ba", "Lejla", true, true, false, null, "Begić", new Guid("a1a1a1a1-0000-0000-0000-000000000001"), "vU//j3TsZkELntInV9wUpCwUYVJmcjrLGGEXgLPNZwE=", "iObaAcHaXVq51zJOd1k4eA==", null, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "lejla.begic" },
                    { new Guid("b2b2b2b2-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ivan@mostar-sport.ba", "Ivan", true, true, false, null, "Marić", new Guid("a1a1a1a1-0000-0000-0000-000000000002"), "ryZEw8BVHyXvX87Bj+QeMKsmzx8PRSNnufi76DdKgu0=", "G5uos2iJ7Kw0wnkMhLwivQ==", null, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ivan.maric" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b2b2b2b2-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b2b2b2b2-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b2b2b2b2-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b2b2b2b2-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-0000-0000-0000-000000000002"));

            migrationBuilder.DropColumn(
                name: "LogoBlobName",
                table: "Organizations");

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoData",
                table: "Organizations",
                type: "varbinary(max)",
                nullable: true);
        }
    }
}
