using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Services.Migrations
{
    /// <inheritdoc />
    public partial class MakePhoneNumberNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "6FF1fth2YvSaobz7aF0Ld28b+jBc92IQ4CRpH55Cw00=", "A5Pk8krdT3jQpHSP/c7j+fS2vOThSVgU+ZxNnS8SRNY=" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "0bDUo4uuorPWTZxrG0vU/tX341BtCutTJuw1T+y4a/Q=", "NUQTxY4haW3vI37SftNFmc0o3Aw0gbZpTWlGcrmcfLc=" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "wUJKwbvAUHi7+hrdaDB1oAy63YTJxhnuEVYY/rhhPdg=", "0+7qf2HHcGTD7zAhbH0cMTz2hUbh+7R1W232UFOfXhY=" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "UBmAZyNXUWFaGeeqSD5SpM9+MrFaYGSthrY/5gkye4M=", "pjqZRPCVk706ENbI3D4c/jb1yW+mU1z+Vfim4GGHGP8=" });
        }
    }
}
