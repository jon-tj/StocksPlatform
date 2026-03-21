using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000000"),
                column: "Currency",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Assets");
        }
    }
}
