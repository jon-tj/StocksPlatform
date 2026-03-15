using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetClassificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subsector",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000000"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("6f8ef50e-e781-0458-8757-dd400efdf483"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("7b917ec4-bbf1-1c5e-aaf0-304481b6e294"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("8b619288-8e7f-bf57-a510-f127f297d90f"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("dd572689-c625-8551-a8cf-65250dfcaf54"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "Id",
                keyValue: new Guid("e83a41d0-6729-3454-9be3-88961116ab75"),
                columns: new[] { "Country", "Region", "Sector", "Subsector" },
                values: new object[] { null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Subsector",
                table: "Assets");
        }
    }
}
