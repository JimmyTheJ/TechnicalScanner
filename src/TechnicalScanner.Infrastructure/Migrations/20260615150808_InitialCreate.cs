using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TechnicalScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "breadth_snapshot",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UniverseName = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Exchange = table.Column<string>(type: "text", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    MetricCode = table.Column<string>(type: "text", nullable: false),
                    TotalValidStocks = table.Column<int>(type: "integer", nullable: false),
                    MatchCount = table.Column<int>(type: "integer", nullable: false),
                    MatchPct = table.Column<decimal>(type: "numeric", nullable: false),
                    MedianValue = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageValue = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_breadth_snapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "indicator_definition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IndicatorType = table.Column<string>(type: "text", nullable: false),
                    Timeframe = table.Column<string>(type: "text", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicator_definition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticker",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    CompanyName = table.Column<string>(type: "text", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    MarketCap = table.Column<decimal>(type: "numeric", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEtf = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticker", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signal_definition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IndicatorDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    rule_json = table.Column<string>(type: "jsonb", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: true),
                    Direction = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_definition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_signal_definition_indicator_definition_IndicatorDefinitionId",
                        column: x => x.IndicatorDefinitionId,
                        principalTable: "indicator_definition",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "indicator_value",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TickerId = table.Column<long>(type: "bigint", nullable: false),
                    IndicatorDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    CandleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Timeframe = table.Column<string>(type: "text", nullable: false),
                    PrimaryValue = table.Column<decimal>(type: "numeric", nullable: true),
                    SecondaryValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TertiaryValue = table.Column<decimal>(type: "numeric", nullable: true),
                    values = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicator_value", x => x.Id);
                    table.ForeignKey(
                        name: "FK_indicator_value_indicator_definition_IndicatorDefinitionId",
                        column: x => x.IndicatorDefinitionId,
                        principalTable: "indicator_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_indicator_value_ticker_TickerId",
                        column: x => x.TickerId,
                        principalTable: "ticker",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_candle",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TickerId = table.Column<long>(type: "bigint", nullable: false),
                    Timeframe = table.Column<string>(type: "text", nullable: false),
                    CandleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric", nullable: true),
                    High = table.Column<decimal>(type: "numeric", nullable: true),
                    Low = table.Column<decimal>(type: "numeric", nullable: true),
                    Close = table.Column<decimal>(type: "numeric", nullable: false),
                    AdjustedClose = table.Column<decimal>(type: "numeric", nullable: true),
                    Volume = table.Column<long>(type: "bigint", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_candle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_candle_ticker_TickerId",
                        column: x => x.TickerId,
                        principalTable: "ticker",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scan_score",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TickerId = table.Column<long>(type: "bigint", nullable: false),
                    ScoreCode = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    components = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_score", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scan_score_ticker_TickerId",
                        column: x => x.TickerId,
                        principalTable: "ticker",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "signal_result",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TickerId = table.Column<long>(type: "bigint", nullable: false),
                    SignalDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    IsMatch = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    values = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_result", x => x.Id);
                    table.ForeignKey(
                        name: "FK_signal_result_signal_definition_SignalDefinitionId",
                        column: x => x.SignalDefinitionId,
                        principalTable: "signal_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_signal_result_ticker_TickerId",
                        column: x => x.TickerId,
                        principalTable: "ticker",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_indicator_definition_Code",
                table: "indicator_definition",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_indicator_value_IndicatorDefinitionId_CandleDate_PrimaryVal~",
                table: "indicator_value",
                columns: new[] { "IndicatorDefinitionId", "CandleDate", "PrimaryValue" });

            migrationBuilder.CreateIndex(
                name: "IX_indicator_value_TickerId_IndicatorDefinitionId_CandleDate",
                table: "indicator_value",
                columns: new[] { "TickerId", "IndicatorDefinitionId", "CandleDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_candle_TickerId_Timeframe_CandleDate",
                table: "price_candle",
                columns: new[] { "TickerId", "Timeframe", "CandleDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scan_score_ScanDateUtc_TickerId_ScoreCode",
                table: "scan_score",
                columns: new[] { "ScanDateUtc", "TickerId", "ScoreCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scan_score_TickerId",
                table: "scan_score",
                column: "TickerId");

            migrationBuilder.CreateIndex(
                name: "IX_signal_definition_Code",
                table: "signal_definition",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signal_definition_IndicatorDefinitionId",
                table: "signal_definition",
                column: "IndicatorDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_signal_result_ScanDateUtc_SignalDefinitionId_IsMatch",
                table: "signal_result",
                columns: new[] { "ScanDateUtc", "SignalDefinitionId", "IsMatch" });

            migrationBuilder.CreateIndex(
                name: "IX_signal_result_ScanDateUtc_TickerId_SignalDefinitionId",
                table: "signal_result",
                columns: new[] { "ScanDateUtc", "TickerId", "SignalDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signal_result_SignalDefinitionId",
                table: "signal_result",
                column: "SignalDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_signal_result_TickerId",
                table: "signal_result",
                column: "TickerId");

            migrationBuilder.CreateIndex(
                name: "IX_ticker_Symbol_Exchange_Country",
                table: "ticker",
                columns: new[] { "Symbol", "Exchange", "Country" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "breadth_snapshot");

            migrationBuilder.DropTable(
                name: "indicator_value");

            migrationBuilder.DropTable(
                name: "price_candle");

            migrationBuilder.DropTable(
                name: "scan_score");

            migrationBuilder.DropTable(
                name: "signal_result");

            migrationBuilder.DropTable(
                name: "signal_definition");

            migrationBuilder.DropTable(
                name: "ticker");

            migrationBuilder.DropTable(
                name: "indicator_definition");
        }
    }
}
