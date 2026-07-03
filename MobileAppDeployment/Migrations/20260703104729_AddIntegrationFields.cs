using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleTeamId",
                table: "AppDeployments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirebaseAndroidConfigPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirebaseIosConfigPath",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IosBundleId",
                table: "AppDeployments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OneSignalAppId",
                table: "AppDeployments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OneSignalRestApiKey",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OneSignalSenderId",
                table: "AppDeployments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppleTeamId",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "FirebaseAndroidConfigPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "FirebaseIosConfigPath",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "IosBundleId",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "OneSignalAppId",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "OneSignalRestApiKey",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "OneSignalSenderId",
                table: "AppDeployments");
        }
    }
}
