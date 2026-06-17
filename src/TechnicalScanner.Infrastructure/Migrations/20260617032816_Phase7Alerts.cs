using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TechnicalScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7Alerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ScanRunId",
                table: "signal_result",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ScanRunId",
                table: "scan_score",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ScanRunId",
                table: "breadth_snapshot",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alert_event",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanRunId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectKey = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    DetectedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_event_scan_run_ScanRunId",
                        column: x => x.ScanRunId,
                        principalTable: "scan_run",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "alert_delivery",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertEventId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    AttemptedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_delivery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_delivery_alert_event_AlertEventId",
                        column: x => x.AlertEventId,
                        principalTable: "alert_event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_signal_result_ScanRunId_TickerId_SignalDefinitionId",
                table: "signal_result",
                columns: new[] { "ScanRunId", "TickerId", "SignalDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_score_ScanRunId_TickerId_ScoreCode",
                table: "scan_score",
                columns: new[] { "ScanRunId", "TickerId", "ScoreCode" });

            migrationBuilder.CreateIndex(
                name: "IX_breadth_snapshot_ScanRunId_MetricCode",
                table: "breadth_snapshot",
                columns: new[] { "ScanRunId", "MetricCode" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_delivery_AlertEventId_Channel",
                table: "alert_delivery",
                columns: new[] { "AlertEventId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_DetectedUtc",
                table: "alert_event",
                column: "DetectedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_EventType_SubjectType_SubjectKey_DetectedUtc",
                table: "alert_event",
                columns: new[] { "EventType", "SubjectType", "SubjectKey", "DetectedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_ScanRunId",
                table: "alert_event",
                column: "ScanRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_breadth_snapshot_scan_run_ScanRunId",
                table: "breadth_snapshot",
                column: "ScanRunId",
                principalTable: "scan_run",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_scan_score_scan_run_ScanRunId",
                table: "scan_score",
                column: "ScanRunId",
                principalTable: "scan_run",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_signal_result_scan_run_ScanRunId",
                table: "signal_result",
                column: "ScanRunId",
                principalTable: "scan_run",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_breadth_snapshot_scan_run_ScanRunId",
                table: "breadth_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_scan_score_scan_run_ScanRunId",
                table: "scan_score");

            migrationBuilder.DropForeignKey(
                name: "FK_signal_result_scan_run_ScanRunId",
                table: "signal_result");

            migrationBuilder.DropTable(
                name: "alert_delivery");

            migrationBuilder.DropTable(
                name: "alert_event");

            migrationBuilder.DropIndex(
                name: "IX_signal_result_ScanRunId_TickerId_SignalDefinitionId",
                table: "signal_result");

            migrationBuilder.DropIndex(
                name: "IX_scan_score_ScanRunId_TickerId_ScoreCode",
                table: "scan_score");

            migrationBuilder.DropIndex(
                name: "IX_breadth_snapshot_ScanRunId_MetricCode",
                table: "breadth_snapshot");

            migrationBuilder.DropColumn(
                name: "ScanRunId",
                table: "signal_result");

            migrationBuilder.DropColumn(
                name: "ScanRunId",
                table: "scan_score");

            migrationBuilder.DropColumn(
                name: "ScanRunId",
                table: "breadth_snapshot");
        }
    }
}
