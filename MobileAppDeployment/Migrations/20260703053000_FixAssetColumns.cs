using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class FixAssetColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AppDeployments" ADD COLUMN IF NOT EXISTS "WebsiteLogoPath" character varying(500);
                ALTER TABLE "AppDeployments" ADD COLUMN IF NOT EXISTS "MobileAppIconPath" character varying(500);
                ALTER TABLE "AppDeployments" ADD COLUMN IF NOT EXISTS "LaunchImagePath" character varying(500);
                ALTER TABLE "AppDeployments" ADD COLUMN IF NOT EXISTS "StoreIconPath" character varying(500);
                ALTER TABLE "AppDeployments" ADD COLUMN IF NOT EXISTS "FeatureGraphicPath" character varying(500);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AppDeployments" DROP COLUMN IF EXISTS "FeatureGraphicPath";
                ALTER TABLE "AppDeployments" DROP COLUMN IF EXISTS "StoreIconPath";
                ALTER TABLE "AppDeployments" DROP COLUMN IF EXISTS "LaunchImagePath";
                ALTER TABLE "AppDeployments" DROP COLUMN IF EXISTS "MobileAppIconPath";
                ALTER TABLE "AppDeployments" DROP COLUMN IF EXISTS "WebsiteLogoPath";
                """);
        }
    }
}
