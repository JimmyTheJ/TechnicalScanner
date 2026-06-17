using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TechnicalScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4FullUniverseScanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scan_run",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TickersProcessed = table.Column<int>(type: "integer", nullable: false),
                    TickersFailed = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticker_operation_failure",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanRunId = table.Column<long>(type: "bigint", nullable: true),
                    TickerId = table.Column<long>(type: "bigint", nullable: true),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticker_operation_failure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticker_operation_failure_scan_run_ScanRunId",
                        column: x => x.ScanRunId,
                        principalTable: "scan_run",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ticker_operation_failure_ticker_TickerId",
                        column: x => x.TickerId,
                        principalTable: "ticker",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_scan_run_StartedUtc",
                table: "scan_run",
                column: "StartedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ticker_operation_failure_ScanRunId_Symbol_Operation",
                table: "ticker_operation_failure",
                columns: new[] { "ScanRunId", "Symbol", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_ticker_operation_failure_TickerId",
                table: "ticker_operation_failure",
                column: "TickerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticker_operation_failure");

            migrationBuilder.DropTable(
                name: "scan_run");
        }
    }
}
