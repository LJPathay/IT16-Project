using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Expiry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "LastPasswordChange" },
                values: new object[] { new DateTime(2026, 4, 12, 17, 22, 56, 420, DateTimeKind.Local).AddTicks(295), new DateTime(2026, 4, 12, 9, 22, 56, 420, DateTimeKind.Utc).AddTicks(305) });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_UserID",
                table: "TrustedDevices",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedDevices");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "LastPasswordChange" },
                values: new object[] { new DateTime(2026, 4, 12, 12, 23, 51, 81, DateTimeKind.Local).AddTicks(2089), new DateTime(2026, 4, 12, 4, 23, 51, 81, DateTimeKind.Utc).AddTicks(2104) });
        }
    }
}
