using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddBullBearCertificateSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BullBearCertificateSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    Gearing = table.Column<double>(type: "REAL", nullable: false),
                    Holders = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BullBearCertificateSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BullBearCertificateSnapshots_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BullBearCertificateSnapshots_AssetId_Date",
                table: "BullBearCertificateSnapshots",
                columns: new[] { "AssetId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BullBearCertificateSnapshots");
        }
    }
}
