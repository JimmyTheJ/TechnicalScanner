using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TechnicalScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase9UniverseBacktestsAndScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsRecalculation",
                table: "indicator_definition",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "profile_backtest_run",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanProfileId = table.Column<long>(type: "bigint", nullable: false),
                    ProfileCode = table.Column<string>(type: "text", nullable: false),
                    ForwardDays = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TickerCount = table.Column<int>(type: "integer", nullable: false),
                    TickersWithEntries = table.Column<int>(type: "integer", nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    MatchDays = table.Column<int>(type: "integer", nullable: false),
                    MedianForwardReturnPct = table.Column<decimal>(type: "numeric", nullable: true),
                    MeanForwardReturnPct = table.Column<decimal>(type: "numeric", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_backtest_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "profile_backtest_hit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileBacktestRunId = table.Column<long>(type: "bigint", nullable: false),
                    TickerId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Close = table.Column<decimal>(type: "numeric", nullable: false),
                    ForwardReturnPct = table.Column<decimal>(type: "numeric", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_backtest_hit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_backtest_hit_profile_backtest_run_ProfileBacktestRu~",
                        column: x => x.ProfileBacktestRunId,
                        principalTable: "profile_backtest_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_backtest_hit_ProfileBacktestRunId_Score",
                table: "profile_backtest_hit",
                columns: new[] { "ProfileBacktestRunId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_profile_backtest_hit_ProfileBacktestRunId_TickerId_EntryDate",
                table: "profile_backtest_hit",
                columns: new[] { "ProfileBacktestRunId", "TickerId", "EntryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_backtest_run_ProfileCode_StartedUtc",
                table: "profile_backtest_run",
                columns: new[] { "ProfileCode", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_backtest_hit");

            migrationBuilder.DropTable(
                name: "profile_backtest_run");

            migrationBuilder.DropColumn(
                name: "NeedsRecalculation",
                table: "indicator_definition");
        }
    }
}
