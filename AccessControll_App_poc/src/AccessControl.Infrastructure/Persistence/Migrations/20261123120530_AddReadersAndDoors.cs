using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadersAndDoors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Readers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SioPanelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Interface = table.Column<int>(type: "int", nullable: false),
                    OsdpAddress = table.Column<short>(type: "smallint", nullable: false),
                    OsdpBaudRate = table.Column<int>(type: "int", nullable: false),
                    SecureChannel = table.Column<bool>(type: "bit", nullable: false),
                    HasKeypad = table.Column<bool>(type: "bit", nullable: false),
                    ScpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readers_Scps_ScpId",
                        column: x => x.ScpId,
                        principalTable: "Scps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Readers_SioPanels_SioPanelId",
                        column: x => x.SioPanelId,
                        principalTable: "SioPanels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Doors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntryReaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExitReaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockOutputId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoorContactInputId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExitButtonInputId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StrikeMinSeconds = table.Column<short>(type: "smallint", nullable: false),
                    StrikeMaxSeconds = table.Column<short>(type: "smallint", nullable: false),
                    HeldOpenSeconds = table.Column<short>(type: "smallint", nullable: false),
                    AccessMode = table.Column<int>(type: "int", nullable: false),
                    ScpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doors_InputPoints_DoorContactInputId",
                        column: x => x.DoorContactInputId,
                        principalTable: "InputPoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doors_InputPoints_ExitButtonInputId",
                        column: x => x.ExitButtonInputId,
                        principalTable: "InputPoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doors_OutputPoints_LockOutputId",
                        column: x => x.LockOutputId,
                        principalTable: "OutputPoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doors_Readers_EntryReaderId",
                        column: x => x.EntryReaderId,
                        principalTable: "Readers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doors_Readers_ExitReaderId",
                        column: x => x.ExitReaderId,
                        principalTable: "Readers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doors_Scps_ScpId",
                        column: x => x.ScpId,
                        principalTable: "Scps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doors_DoorContactInputId",
                table: "Doors",
                column: "DoorContactInputId",
                unique: true,
                filter: "[DoorContactInputId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Doors_EntryReaderId",
                table: "Doors",
                column: "EntryReaderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doors_ExitButtonInputId",
                table: "Doors",
                column: "ExitButtonInputId",
                unique: true,
                filter: "[ExitButtonInputId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Doors_ExitReaderId",
                table: "Doors",
                column: "ExitReaderId",
                unique: true,
                filter: "[ExitReaderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Doors_LockOutputId",
                table: "Doors",
                column: "LockOutputId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doors_ScpId_Name",
                table: "Doors",
                columns: new[] { "ScpId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doors_ScpId_Number",
                table: "Doors",
                columns: new[] { "ScpId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Readers_ScpId_Name",
                table: "Readers",
                columns: new[] { "ScpId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Readers_SioPanelId_Number",
                table: "Readers",
                columns: new[] { "SioPanelId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Doors");

            migrationBuilder.DropTable(
                name: "Readers");
        }
    }
}
