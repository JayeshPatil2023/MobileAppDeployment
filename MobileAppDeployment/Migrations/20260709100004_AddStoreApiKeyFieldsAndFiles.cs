using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreApiKeyFieldsAndFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleAuthKeyPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppleIssuerId",
                table: "AppDeployments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppleKeyId",
                table: "AppDeployments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlayStoreKeyPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppleAuthKeyPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "AppleIssuerId",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "AppleKeyId",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "PlayStoreKeyPath",
                table: "AppDeployments");
        }
    }
}
