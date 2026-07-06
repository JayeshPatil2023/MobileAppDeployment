using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidMobileConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AndroidPackageName",
                table: "AppDeployments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GooglePlayListingUrl",
                table: "AppDeployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AndroidPackageName",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "GooglePlayListingUrl",
                table: "AppDeployments");
        }
    }
}
