using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Core.Models.ViewModels;

/// <summary>
/// Read-only view model for the deployment Details page.
/// </summary>
/// <remarks>
/// Excludes sensitive fields such as <c>OneSignalRestApiKey</c>; the Details view
/// shows a masked placeholder instead.
/// </remarks>
public class AppDeploymentDetailsViewModel
{
    public int Id { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string OrganizationUnit { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string LocalityName { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string AppleName { get; set; } = string.Empty;
    public string AppleSubtitle { get; set; } = string.Empty;
    public string? ApplePromotionalText { get; set; }
    public string AppleDescription { get; set; } = string.Empty;
    public string AppleKeywords { get; set; } = string.Empty;
    public string? AppleSupportUrl { get; set; }
    public string? AppleMarketingUrl { get; set; }
    public string AppleCopyright { get; set; } = string.Empty;
    public string ContactFirstName { get; set; } = string.Empty;
    public string ContactLastName { get; set; } = string.Empty;
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string ContactEmailAddress { get; set; } = string.Empty;
    public string PrivacyPolicyUrl { get; set; } = string.Empty;
    public string? MobileAppIconPath { get; set; }
    public string? LaunchImagePath { get; set; }
    public string? StoreIconPath { get; set; }
    public string? FeatureGraphicPath { get; set; }
    public string AndroidPackageName { get; set; } = string.Empty;
    public string? GooglePlayListingUrl { get; set; }
    public string? PlayStoreKeyPath { get; set; }
    public string IosBundleId { get; set; } = string.Empty;
    public string AppleTeamId { get; set; } = string.Empty;
    public string AppleIssuerId { get; set; } = string.Empty;
    public string AppleKeyId { get; set; } = string.Empty;
    public string? AppleAuthKeyPath { get; set; }
    public string DomainUrl { get; set; } = string.Empty;
    public string OneSignalSenderId { get; set; } = string.Empty;
    public string OneSignalAppId { get; set; } = string.Empty;
    public string? FirebaseIosConfigPath { get; set; }
    public string? FirebaseAndroidConfigPath { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// Maps a domain entity to a details view model without sensitive fields.
    /// </summary>
    /// <param name="entity">Deployment loaded from the database.</param>
    /// <returns>A read-only details view model.</returns>
    public static AppDeploymentDetailsViewModel FromEntity(AppDeployment entity)
    {
        return new AppDeploymentDetailsViewModel
        {
            Id = entity.Id,
            CommonName = entity.CommonName,
            OrganizationUnit = entity.OrganizationUnit,
            OrganizationName = entity.OrganizationName,
            LocalityName = entity.LocalityName,
            StateName = entity.StateName,
            Country = entity.Country,
            AdminEmail = entity.AdminEmail,
            AppName = entity.AppName,
            ShortDescription = entity.ShortDescription,
            FullDescription = entity.FullDescription,
            AppleName = entity.AppleName,
            AppleSubtitle = entity.AppleSubtitle,
            ApplePromotionalText = entity.ApplePromotionalText,
            AppleDescription = entity.AppleDescription,
            AppleKeywords = entity.AppleKeywords,
            AppleSupportUrl = entity.AppleSupportUrl,
            AppleMarketingUrl = entity.AppleMarketingUrl,
            AppleCopyright = entity.AppleCopyright,
            ContactFirstName = entity.ContactFirstName,
            ContactLastName = entity.ContactLastName,
            ContactPhoneNumber = entity.ContactPhoneNumber,
            ContactEmailAddress = entity.ContactEmailAddress,
            PrivacyPolicyUrl = entity.PrivacyPolicyUrl,
            MobileAppIconPath = entity.MobileAppIconPath,
            LaunchImagePath = entity.LaunchImagePath,
            StoreIconPath = entity.StoreIconPath,
            FeatureGraphicPath = entity.FeatureGraphicPath,
            AndroidPackageName = entity.AndroidPackageName,
            GooglePlayListingUrl = entity.GooglePlayListingUrl,
            PlayStoreKeyPath = entity.PlayStoreKeyPath,
            IosBundleId = entity.IosBundleId,
            AppleTeamId = entity.AppleTeamId,
            AppleIssuerId = entity.AppleIssuerId,
            AppleKeyId = entity.AppleKeyId,
            AppleAuthKeyPath = entity.AppleAuthKeyPath,
            DomainUrl = entity.DomainUrl,
            OneSignalSenderId = entity.OneSignalSenderId,
            OneSignalAppId = entity.OneSignalAppId,
            FirebaseIosConfigPath = entity.FirebaseIosConfigPath,
            FirebaseAndroidConfigPath = entity.FirebaseAndroidConfigPath,
            CreatedDate = entity.CreatedDate,
            ModifiedDate = entity.ModifiedDate
        };
    }
}
