using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeatureGraphicPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaunchImagePath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileAppIconPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreIconPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteLogoPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeatureGraphicPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "LaunchImagePath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "MobileAppIconPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "StoreIconPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "WebsiteLogoPath",
                table: "AppDeployments");
        }
    }
}
