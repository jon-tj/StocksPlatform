using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddFundamentalSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundamentalSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TrailingEps = table.Column<double>(type: "REAL", nullable: true),
                    NormalizedEps = table.Column<double>(type: "REAL", nullable: true),
                    TrailingRevenue = table.Column<double>(type: "REAL", nullable: true),
                    TrailingEbitda = table.Column<double>(type: "REAL", nullable: true),
                    TrailingOperatingIncome = table.Column<double>(type: "REAL", nullable: true),
                    TrailingNetIncome = table.Column<double>(type: "REAL", nullable: true),
                    TrailingDividendPerShare = table.Column<double>(type: "REAL", nullable: true),
                    RevenueGrowthRate = table.Column<double>(type: "REAL", nullable: true),
                    CurrentPrice = table.Column<double>(type: "REAL", nullable: true),
                    GrahamValue = table.Column<double>(type: "REAL", nullable: true),
                    DcfValue = table.Column<double>(type: "REAL", nullable: true),
                    EarningsMultipleValue = table.Column<double>(type: "REAL", nullable: true),
                    ConsensusValue = table.Column<double>(type: "REAL", nullable: true),
                    PeerMeanPe = table.Column<double>(type: "REAL", nullable: true),
                    PeerSymbols = table.Column<string>(type: "TEXT", nullable: true),
                    AssetCurrentPe = table.Column<double>(type: "REAL", nullable: true),
                    FundamentalDelta = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundamentalSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundamentalSnapshots_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalSnapshots_AssetId_Date",
                table: "FundamentalSnapshots",
                columns: new[] { "AssetId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundamentalSnapshots");
        }
    }
}
