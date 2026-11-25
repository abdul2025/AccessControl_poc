using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishAndProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedVersion",
                table: "Scps");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Scps");

            migrationBuilder.AddColumn<short>(
                name: "ExitAcrNumber",
                table: "Doors",
                type: "smallint",
                nullable: true);

            // Existing doors with an exit reader get their paired ACR the same way the domain assigns it: per controller,
            // in door-number order, the lowest ACR number (0-63) not used by any door number or earlier exit ACR.
            migrationBuilder.Sql("""
                DECLARE @DoorId uniqueidentifier, @ScpId uniqueidentifier, @Acr smallint;
                DECLARE exit_doors CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [Id], [ScpId] FROM [Doors] WHERE [ExitReaderId] IS NOT NULL ORDER BY [ScpId], [Number];
                OPEN exit_doors;
                FETCH NEXT FROM exit_doors INTO @DoorId, @ScpId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SELECT @Acr = MIN(candidate.[Number])
                    FROM (SELECT TOP (64) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS smallint) AS [Number]
                          FROM sys.all_objects) AS candidate
                    WHERE NOT EXISTS (SELECT 1 FROM [Doors] AS used
                                      WHERE used.[ScpId] = @ScpId
                                        AND (used.[Number] = candidate.[Number] OR used.[ExitAcrNumber] = candidate.[Number]));

                    UPDATE [Doors] SET [ExitAcrNumber] = @Acr WHERE [Id] = @DoorId;
                    FETCH NEXT FROM exit_doors INTO @DoorId, @ScpId;
                END
                CLOSE exit_doors;
                DEALLOCATE exit_doors;
                """);

            migrationBuilder.CreateTable(
                name: "ScpApplyRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    FailedOrder = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScpApplyRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScpApplyRuns_Scps_ScpId",
                        column: x => x.ScpId,
                        principalTable: "Scps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScpConfigVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CompiledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommandHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CompileError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScpConfigVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScpConfigVersions_Scps_ScpId",
                        column: x => x.ScpId,
                        principalTable: "Scps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScpConnectionStates",
                columns: table => new
                {
                    ScpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    SessionScpNumber = table.Column<int>(type: "int", nullable: true),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MacAddress = table.Column<string>(type: "varchar(17)", unicode: false, maxLength: 17, nullable: true),
                    ReportedScpId = table.Column<int>(type: "int", nullable: true),
                    ClockOffsetSeconds = table.Column<double>(type: "float", nullable: true),
                    LastIdReportAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppliedVersion = table.Column<int>(type: "int", nullable: true),
                    ApplyState = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScpConnectionStates", x => x.ScpId);
                    table.ForeignKey(
                        name: "FK_ScpConnectionStates_Scps_ScpId",
                        column: x => x.ScpId,
                        principalTable: "Scps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScpApplyResults",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CommandNumber = table.Column<short>(type: "smallint", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tag = table.Column<int>(type: "int", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScpApplyResults", x => new { x.RunId, x.Order });
                    table.ForeignKey(
                        name: "FK_ScpApplyResults_ScpApplyRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ScpApplyRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScpConfigCommands",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CommandNumber = table.Column<short>(type: "smallint", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Text = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScpConfigCommands", x => new { x.VersionId, x.Order });
                    table.ForeignKey(
                        name: "FK_ScpConfigCommands_ScpConfigVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ScpConfigVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doors_ScpId_ExitAcrNumber",
                table: "Doors",
                columns: new[] { "ScpId", "ExitAcrNumber" },
                unique: true,
                filter: "[ExitAcrNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScpApplyRuns_ScpId_StartedAtUtc",
                table: "ScpApplyRuns",
                columns: new[] { "ScpId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScpConfigVersions_ScpId_Version",
                table: "ScpConfigVersions",
                columns: new[] { "ScpId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScpApplyResults");

            migrationBuilder.DropTable(
                name: "ScpConfigCommands");

            migrationBuilder.DropTable(
                name: "ScpConnectionStates");

            migrationBuilder.DropTable(
                name: "ScpApplyRuns");

            migrationBuilder.DropTable(
                name: "ScpConfigVersions");

            migrationBuilder.DropIndex(
                name: "IX_Doors_ScpId_ExitAcrNumber",
                table: "Doors");

            migrationBuilder.DropColumn(
                name: "ExitAcrNumber",
                table: "Doors");

            migrationBuilder.AddColumn<int>(
                name: "AppliedVersion",
                table: "Scps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Scps",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
