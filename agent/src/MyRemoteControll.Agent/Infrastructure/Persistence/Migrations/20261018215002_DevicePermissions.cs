using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyRemoteControll.Agent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DevicePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevicePermissions",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PermissionKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePermissions", x => new { x.DeviceId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_DevicePermissions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePermissions");
        }
    }
}
