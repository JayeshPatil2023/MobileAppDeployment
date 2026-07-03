-- =============================================
-- Mobile App Deployment Database Script
-- Run this script against SQL Server to create
-- the table and CRUD stored procedures.
-- =============================================

-- Create database (optional - uncomment if needed)
-- CREATE DATABASE MobileAppDeploymentDB;
-- GO
-- USE MobileAppDeploymentDB;
-- GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppDeployments')
BEGIN
    CREATE TABLE dbo.AppDeployments
    (
        Id                      INT IDENTITY(1,1) NOT NULL,
        CommonName              NVARCHAR(255)     NOT NULL,
        OrganizationUnit        NVARCHAR(255)     NOT NULL,
        OrganizationName        NVARCHAR(255)     NOT NULL,
        LocalityName            NVARCHAR(255)     NOT NULL,
        StateName               NVARCHAR(255)     NOT NULL,
        Country                 NVARCHAR(2)       NOT NULL,
        AdminEmail              NVARCHAR(255)     NOT NULL,
        AppName                 NVARCHAR(30)      NOT NULL,
        ShortDescription        NVARCHAR(80)      NOT NULL,
        FullDescription         NVARCHAR(4000)    NOT NULL,
        AppleName               NVARCHAR(30)      NOT NULL,
        AppleSubtitle           NVARCHAR(30)      NOT NULL,
        ApplePromotionalText    NVARCHAR(170)     NULL,
        AppleDescription        NVARCHAR(4000)    NOT NULL,
        AppleKeywords           NVARCHAR(100)     NOT NULL,
        AppleSupportUrl         NVARCHAR(500)     NULL,
        AppleMarketingUrl       NVARCHAR(500)     NULL,
        AppleCopyright          NVARCHAR(255)     NOT NULL,
        ContactFirstName        NVARCHAR(100)     NOT NULL,
        ContactLastName         NVARCHAR(100)     NOT NULL,
        ContactPhoneNumber      NVARCHAR(50)      NOT NULL,
        ContactEmailAddress     NVARCHAR(255)     NOT NULL,
        PrivacyPolicyUrl        NVARCHAR(500)     NOT NULL,
        CreatedDate             DATETIME2         NOT NULL CONSTRAINT DF_AppDeployments_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate            DATETIME2         NULL,
        CONSTRAINT PK_AppDeployments PRIMARY KEY CLUSTERED (Id)
    );
END
GO

-- =============================================
-- usp_AppDeployment_GetAll
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_AppDeployment_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id, CommonName, OrganizationUnit, OrganizationName,
        LocalityName, StateName, Country, AdminEmail,
        AppName, ShortDescription, FullDescription,
        AppleName, AppleSubtitle, ApplePromotionalText,
        AppleDescription, AppleKeywords, AppleSupportUrl,
        AppleMarketingUrl, AppleCopyright,
        ContactFirstName, ContactLastName, ContactPhoneNumber,
        ContactEmailAddress, PrivacyPolicyUrl,
        CreatedDate, ModifiedDate
    FROM dbo.AppDeployments
    ORDER BY CreatedDate DESC;
END
GO

-- =============================================
-- usp_AppDeployment_GetById
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_AppDeployment_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id, CommonName, OrganizationUnit, OrganizationName,
        LocalityName, StateName, Country, AdminEmail,
        AppName, ShortDescription, FullDescription,
        AppleName, AppleSubtitle, ApplePromotionalText,
        AppleDescription, AppleKeywords, AppleSupportUrl,
        AppleMarketingUrl, AppleCopyright,
        ContactFirstName, ContactLastName, ContactPhoneNumber,
        ContactEmailAddress, PrivacyPolicyUrl,
        CreatedDate, ModifiedDate
    FROM dbo.AppDeployments
    WHERE Id = @Id;
END
GO

-- =============================================
-- usp_AppDeployment_Insert
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_AppDeployment_Insert
    @Id                      INT OUTPUT,
    @CommonName              NVARCHAR(255),
    @OrganizationUnit        NVARCHAR(255),
    @OrganizationName        NVARCHAR(255),
    @LocalityName            NVARCHAR(255),
    @StateName               NVARCHAR(255),
    @Country                 NVARCHAR(2),
    @AdminEmail              NVARCHAR(255),
    @AppName                 NVARCHAR(30),
    @ShortDescription        NVARCHAR(80),
    @FullDescription         NVARCHAR(4000),
    @AppleName               NVARCHAR(30),
    @AppleSubtitle           NVARCHAR(30),
    @ApplePromotionalText    NVARCHAR(170) = NULL,
    @AppleDescription        NVARCHAR(4000),
    @AppleKeywords           NVARCHAR(100),
    @AppleSupportUrl         NVARCHAR(500) = NULL,
    @AppleMarketingUrl       NVARCHAR(500) = NULL,
    @AppleCopyright          NVARCHAR(255),
    @ContactFirstName        NVARCHAR(100),
    @ContactLastName         NVARCHAR(100),
    @ContactPhoneNumber      NVARCHAR(50),
    @ContactEmailAddress     NVARCHAR(255),
    @PrivacyPolicyUrl        NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.AppDeployments
    (
        CommonName, OrganizationUnit, OrganizationName,
        LocalityName, StateName, Country, AdminEmail,
        AppName, ShortDescription, FullDescription,
        AppleName, AppleSubtitle, ApplePromotionalText,
        AppleDescription, AppleKeywords, AppleSupportUrl,
        AppleMarketingUrl, AppleCopyright,
        ContactFirstName, ContactLastName, ContactPhoneNumber,
        ContactEmailAddress, PrivacyPolicyUrl
    )
    VALUES
    (
        @CommonName, @OrganizationUnit, @OrganizationName,
        @LocalityName, @StateName, @Country, @AdminEmail,
        @AppName, @ShortDescription, @FullDescription,
        @AppleName, @AppleSubtitle, @ApplePromotionalText,
        @AppleDescription, @AppleKeywords, @AppleSupportUrl,
        @AppleMarketingUrl, @AppleCopyright,
        @ContactFirstName, @ContactLastName, @ContactPhoneNumber,
        @ContactEmailAddress, @PrivacyPolicyUrl
    );

    SET @Id = SCOPE_IDENTITY();
END
GO

-- =============================================
-- usp_AppDeployment_Update
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_AppDeployment_Update
    @Id                      INT,
    @CommonName              NVARCHAR(255),
    @OrganizationUnit        NVARCHAR(255),
    @OrganizationName        NVARCHAR(255),
    @LocalityName            NVARCHAR(255),
    @StateName               NVARCHAR(255),
    @Country                 NVARCHAR(2),
    @AdminEmail              NVARCHAR(255),
    @AppName                 NVARCHAR(30),
    @ShortDescription        NVARCHAR(80),
    @FullDescription         NVARCHAR(4000),
    @AppleName               NVARCHAR(30),
    @AppleSubtitle           NVARCHAR(30),
    @ApplePromotionalText    NVARCHAR(170) = NULL,
    @AppleDescription        NVARCHAR(4000),
    @AppleKeywords           NVARCHAR(100),
    @AppleSupportUrl         NVARCHAR(500) = NULL,
    @AppleMarketingUrl       NVARCHAR(500) = NULL,
    @AppleCopyright          NVARCHAR(255),
    @ContactFirstName        NVARCHAR(100),
    @ContactLastName         NVARCHAR(100),
    @ContactPhoneNumber      NVARCHAR(50),
    @ContactEmailAddress     NVARCHAR(255),
    @PrivacyPolicyUrl        NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AppDeployments
    SET
        CommonName           = @CommonName,
        OrganizationUnit     = @OrganizationUnit,
        OrganizationName     = @OrganizationName,
        LocalityName         = @LocalityName,
        StateName            = @StateName,
        Country              = @Country,
        AdminEmail           = @AdminEmail,
        AppName              = @AppName,
        ShortDescription     = @ShortDescription,
        FullDescription      = @FullDescription,
        AppleName            = @AppleName,
        AppleSubtitle        = @AppleSubtitle,
        ApplePromotionalText = @ApplePromotionalText,
        AppleDescription     = @AppleDescription,
        AppleKeywords        = @AppleKeywords,
        AppleSupportUrl      = @AppleSupportUrl,
        AppleMarketingUrl    = @AppleMarketingUrl,
        AppleCopyright       = @AppleCopyright,
        ContactFirstName     = @ContactFirstName,
        ContactLastName      = @ContactLastName,
        ContactPhoneNumber   = @ContactPhoneNumber,
        ContactEmailAddress  = @ContactEmailAddress,
        PrivacyPolicyUrl     = @PrivacyPolicyUrl,
        ModifiedDate         = SYSUTCDATETIME()
    WHERE Id = @Id;
END
GO

-- =============================================
-- usp_AppDeployment_Delete
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_AppDeployment_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.AppDeployments
    WHERE Id = @Id;
END
GO
