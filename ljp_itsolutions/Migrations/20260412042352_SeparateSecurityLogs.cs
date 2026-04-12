using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class SeparateSecurityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityLogs",
                columns: table => new
                {
                    SecurityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityLogs", x => x.SecurityID);
                    table.ForeignKey(
                        name: "FK_SecurityLogs_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "LastPasswordChange" },
                values: new object[] { new DateTime(2026, 4, 12, 12, 23, 51, 81, DateTimeKind.Local).AddTicks(2089), new DateTime(2026, 4, 12, 4, 23, 51, 81, DateTimeKind.Utc).AddTicks(2104) });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_UserID",
                table: "SecurityLogs",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityLogs");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "LastPasswordChange" },
                values: new object[] { new DateTime(2026, 4, 12, 11, 58, 17, 204, DateTimeKind.Local).AddTicks(2622), new DateTime(2026, 4, 12, 3, 58, 17, 204, DateTimeKind.Utc).AddTicks(2632) });
        }
    }
}
