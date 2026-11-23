using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSioPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InputPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Contact = table.Column<int>(type: "int", nullable: false),
                    Supervised = table.Column<bool>(type: "bit", nullable: false),
                    Monitored = table.Column<bool>(type: "bit", nullable: false),
                    Dedicated = table.Column<int>(type: "int", nullable: false),
                    SioPanelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InputPoints_SioPanels_SioPanelId",
                        column: x => x.SioPanelId,
                        principalTable: "SioPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutputPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PulseSeconds = table.Column<short>(type: "smallint", nullable: false),
                    SioPanelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutputPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutputPoints_SioPanels_SioPanelId",
                        column: x => x.SioPanelId,
                        principalTable: "SioPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InputPoints_SioPanelId_Number",
                table: "InputPoints",
                columns: new[] { "SioPanelId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutputPoints_SioPanelId_Number",
                table: "OutputPoints",
                columns: new[] { "SioPanelId", "Number" },
                unique: true);

            // Backfill: SIO rows created before this migration get their physical points. Mirrors
            // HardwareCatalog (general inputs IN 1..n, then Tamper / AC fail / Battery fail; Relay 1..n) and the defaults
            // in InputPoint.Create / OutputPoint.Create. Values: Contact 1 = NC, 2 = NO; Dedicated 1 = None,
            // 2 = Tamper, 3 = AcFail, 4 = BatteryFail. Model codes: 196 X1100 internal, 193 X100, 194 X200, 195 X300.
            // Idempotent: panels that already have points are skipped.
            migrationBuilder.Sql("""
                WITH Catalog (Model, GeneralInputs, Relays) AS (
                    SELECT Model, GeneralInputs, Relays FROM (VALUES (196, 4, 4), (193, 4, 4), (194, 16, 2), (195, 2, 12)) AS v (Model, GeneralInputs, Relays)
                ),
                Numbers (N) AS (
                    SELECT TOP (32) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS smallint) FROM sys.all_objects
                )
                INSERT INTO InputPoints (Id, Number, Name, Contact, Supervised, Monitored, Dedicated, SioPanelId)
                SELECT NEWID(), n.N,
                       CASE WHEN n.N < c.GeneralInputs THEN CONCAT('IN ', n.N + 1)
                            WHEN n.N = c.GeneralInputs THEN 'Tamper'
                            WHEN n.N = c.GeneralInputs + 1 THEN 'AC fail'
                            ELSE 'Battery fail' END,
                       CASE WHEN n.N < c.GeneralInputs THEN 2 ELSE 1 END,
                       0,
                       CASE WHEN n.N < c.GeneralInputs THEN 0 ELSE 1 END,
                       CASE WHEN n.N < c.GeneralInputs THEN 1
                            WHEN n.N = c.GeneralInputs THEN 2
                            WHEN n.N = c.GeneralInputs + 1 THEN 3
                            ELSE 4 END,
                       p.Id
                FROM SioPanels AS p
                JOIN Catalog AS c ON c.Model = p.Model
                JOIN Numbers AS n ON n.N < c.GeneralInputs + 3
                WHERE NOT EXISTS (SELECT 1 FROM InputPoints AS i WHERE i.SioPanelId = p.Id);
                """);

            migrationBuilder.Sql("""
                WITH Catalog (Model, Relays) AS (
                    SELECT Model, Relays FROM (VALUES (196, 4), (193, 4), (194, 2), (195, 12)) AS v (Model, Relays)
                ),
                Numbers (N) AS (
                    SELECT TOP (32) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS smallint) FROM sys.all_objects
                )
                INSERT INTO OutputPoints (Id, Number, Name, PulseSeconds, SioPanelId)
                SELECT NEWID(), n.N, CONCAT('Relay ', n.N + 1), 1, p.Id
                FROM SioPanels AS p
                JOIN Catalog AS c ON c.Model = p.Model
                JOIN Numbers AS n ON n.N < c.Relays
                WHERE NOT EXISTS (SELECT 1 FROM OutputPoints AS o WHERE o.SioPanelId = p.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InputPoints");

            migrationBuilder.DropTable(
                name: "OutputPoints");
        }
    }
}
