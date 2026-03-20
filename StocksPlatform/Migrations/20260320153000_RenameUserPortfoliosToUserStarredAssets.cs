using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StocksPlatform.Migrations;

[Migration("20260320153000_RenameUserPortfoliosToUserStarredAssets")]
public partial class RenameUserPortfoliosToUserStarredAssets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "UserPortfolios",
            newName: "UserStarredAssets");

        migrationBuilder.CreateIndex(
            name: "IX_UserStarredAssets_UserId_AssetId",
            table: "UserStarredAssets",
            columns: new[] { "UserId", "AssetId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserStarredAssets_UserId_AssetId",
            table: "UserStarredAssets");

        migrationBuilder.RenameTable(
            name: "UserStarredAssets",
            newName: "UserPortfolios");
    }
}
