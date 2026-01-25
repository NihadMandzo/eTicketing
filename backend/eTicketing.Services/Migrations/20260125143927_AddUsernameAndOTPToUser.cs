using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Services.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameAndOTPToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OTP",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OTPExpiration",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "OTP", "OTPExpiration", "PasswordHash", "PasswordSalt", "Username" },
                values: new object[] { null, null, "wUJKwbvAUHi7+hrdaDB1oAy63YTJxhnuEVYY/rhhPdg=", "0+7qf2HHcGTD7zAhbH0cMTz2hUbh+7R1W232UFOfXhY=", "" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "OTP", "OTPExpiration", "PasswordHash", "PasswordSalt", "Username" },
                values: new object[] { null, null, "UBmAZyNXUWFaGeeqSD5SpM9+MrFaYGSthrY/5gkye4M=", "pjqZRPCVk706ENbI3D4c/jb1yW+mU1z+Vfim4GGHGP8=", "" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OTP",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OTPExpiration",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "6hcxIJO1EWqgFsQh7clsHhQXfqnmPgXebA2anKWDjQ0=", "t7xiN2v96Nbf604HrdVZOA94IiOFYS7+lLE9h4gJux8=" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "JPujZ6IJUV6/hNmVsdqf2vnqDADJgB7STpIJP7mvDR4=", "HLgs9KjKo2y02rfK5b7P8Ewayl5aDpWynB0hYZ9hxCI=" });
        }
    }
}
