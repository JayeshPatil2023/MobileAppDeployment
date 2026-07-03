using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MobileAppDeployment.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDeployments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommonName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OrganizationUnit = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LocalityName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StateName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AppName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FullDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AppleName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AppleSubtitle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApplePromotionalText = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: true),
                    AppleDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AppleKeywords = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppleSupportUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppleMarketingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppleCopyright = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContactFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactPhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactEmailAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PrivacyPolicyUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDeployments", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDeployments");
        }
    }
}
