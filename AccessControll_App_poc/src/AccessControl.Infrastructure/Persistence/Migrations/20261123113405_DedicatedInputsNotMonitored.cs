using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DedicatedInputsNotMonitored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only (no schema change). Tamper / AC fail / Battery fail inputs (Dedicated 2, 3, 4) are no longer
            // monitored by default: an unwired normally-closed input reads "open" and would raise a permanent alarm.
            // Users tick Monitored once the installer confirms the wiring (docs/plans/tamper-ac-battery-inputs.md, option A).
            // Applies to rows created by the AddSioPoints backfill and by the earlier InputPoint default.
            migrationBuilder.Sql("UPDATE InputPoints SET Monitored = 0 WHERE Dedicated IN (2, 3, 4) AND Monitored = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the previous default for dedicated inputs.
            migrationBuilder.Sql("UPDATE InputPoints SET Monitored = 1 WHERE Dedicated IN (2, 3, 4) AND Monitored = 0;");
        }
    }
}
