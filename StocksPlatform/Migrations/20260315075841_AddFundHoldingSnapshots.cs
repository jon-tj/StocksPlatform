using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddFundHoldingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FundHoldingSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeanFundPercentage = table.Column<double>(type: "REAL", nullable: false),
                    MedianFundPercentage = table.Column<double>(type: "REAL", nullable: false),
                    NumFundsRepresented = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundHoldingSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundHoldingSnapshots_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundPortfolioMetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FundIsin = table.Column<string>(type: "TEXT", nullable: false),
                    LastPortfolioDate = table.Column<string>(type: "TEXT", nullable: true),
                    LastRunDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundPortfolioMetas", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000000"),
                column: "Isin",
                value: null);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("6f8ef50e-e781-0458-8757-dd400efdf483"),
                column: "Isin",
                value: null);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("7b917ec4-bbf1-1c5e-aaf0-304481b6e294"),
                column: "Isin",
                value: null);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("8b619288-8e7f-bf57-a510-f127f297d90f"),
                column: "Isin",
                value: null);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("dd572689-c625-8551-a8cf-65250dfcaf54"),
                column: "Isin",
                value: null);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("e83a41d0-6729-3454-9be3-88961116ab75"),
                column: "Isin",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_FundHoldingSnapshots_AssetId_Date",
                table: "FundHoldingSnapshots",
                columns: new[] { "AssetId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundPortfolioMetas_FundIsin",
                table: "FundPortfolioMetas",
                column: "FundIsin",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundHoldingSnapshots");

            migrationBuilder.DropTable(
                name: "FundPortfolioMetas");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "Assets");
        }
    }
}
