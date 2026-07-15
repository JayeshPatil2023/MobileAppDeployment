using System.ComponentModel.DataAnnotations;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Core.Models.ViewModels;

/// <summary>
/// MVC form binding model for Create and Edit deployment actions.
/// </summary>
/// <remarks>
/// Decouples the <see cref="AppDeployment"/> domain entity from form posts so sensitive
/// fields (e.g. <see cref="OneSignalRestApiKey"/>) are never returned to the browser
/// via entity mutation.
/// </remarks>
public class AppDeploymentFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Common Name is required.")]
    [Display(Name = "Common Name (CN)")]
    [StringLength(255)]
    public string CommonName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Organization Unit is required.")]
    [Display(Name = "Organization Unit (OU)")]
    [StringLength(255)]
    public string OrganizationUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Organization Name is required.")]
    [Display(Name = "Organization Name (O)")]
    [StringLength(255)]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Locality Name is required.")]
    [Display(Name = "Locality / City (L)")]
    [StringLength(255)]
    public string LocalityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "State Name is required.")]
    [Display(Name = "State / Province (S)")]
    [StringLength(255)]
    public string StateName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [Display(Name = "Country (C)")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country must be a two-letter code (e.g. US).")]
    public string Country { get; set; } = "US";

    [Required(ErrorMessage = "Admin email is required.")]
    [Display(Name = "Admin Email")]
    [EmailAddress]
    [StringLength(255)]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "App Name is required.")]
    [Display(Name = "App Name")]
    [StringLength(30, ErrorMessage = "App Name must be 30 characters or less.")]
    public string AppName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Short description is required.")]
    [Display(Name = "Short Description")]
    [StringLength(80, ErrorMessage = "Short description must be 80 characters or less.")]
    public string ShortDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full description is required.")]
    [Display(Name = "Full Description")]
    [StringLength(4000, ErrorMessage = "Full description must be 4000 characters or less.")]
    public string FullDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Apple listing name is required.")]
    [Display(Name = "Name")]
    [StringLength(30, ErrorMessage = "Name must be 30 characters or less.")]
    public string AppleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subtitle is required.")]
    [Display(Name = "Subtitle")]
    [StringLength(30, ErrorMessage = "Subtitle must be 30 characters or less.")]
    public string AppleSubtitle { get; set; } = string.Empty;

    [Display(Name = "Promotional Text")]
    [StringLength(170, ErrorMessage = "Promotional text must be 170 characters or less.")]
    public string? ApplePromotionalText { get; set; }

    [Required(ErrorMessage = "Apple description is required.")]
    [Display(Name = "Description")]
    [StringLength(4000, ErrorMessage = "Description must be 4000 characters or less.")]
    public string AppleDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Keywords are required.")]
    [Display(Name = "Keywords")]
    [StringLength(100, ErrorMessage = "Keywords must be 100 characters or less.")]
    public string AppleKeywords { get; set; } = string.Empty;

    [Display(Name = "Support URL")]
    [Url(ErrorMessage = "Please enter a valid URL.")]
    [StringLength(500)]
    public string? AppleSupportUrl { get; set; }

    [Display(Name = "Marketing URL")]
    [Url(ErrorMessage = "Please enter a valid URL.")]
    [StringLength(500)]
    public string? AppleMarketingUrl { get; set; }

    [Required(ErrorMessage = "Copyright is required.")]
    [Display(Name = "Copyright")]
    [StringLength(255)]
    public string AppleCopyright { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [Display(Name = "First Name")]
    [StringLength(100)]
    public string ContactFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [Display(Name = "Last Name")]
    [StringLength(100)]
    public string ContactLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Display(Name = "Phone Number")]
    [Phone]
    [StringLength(50)]
    public string ContactPhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact email is required.")]
    [Display(Name = "Email Address")]
    [EmailAddress]
    [StringLength(255)]
    public string ContactEmailAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Privacy Policy URL is required.")]
    [Display(Name = "Privacy Policy URL")]
    [Url(ErrorMessage = "Please enter a valid URL.")]
    [StringLength(500)]
    public string PrivacyPolicyUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string? MobileAppIconPath { get; set; }

    [StringLength(500)]
    public string? LaunchImagePath { get; set; }

    [StringLength(500)]
    public string? StoreIconPath { get; set; }

    [StringLength(500)]
    public string? FeatureGraphicPath { get; set; }

    [Required(ErrorMessage = "Package name is required.")]
    [Display(Name = "Package name")]
    [StringLength(255)]
    public string AndroidPackageName { get; set; } = string.Empty;

    [Display(Name = "Google Play listing URL")]
    [StringLength(500)]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string? GooglePlayListingUrl { get; set; }

    [StringLength(500)]
    public string? PlayStoreKeyPath { get; set; }

    [Required(ErrorMessage = "iOS Bundle ID is required.")]
    [Display(Name = "iOS Bundle ID")]
    [StringLength(255)]
    public string IosBundleId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Apple Team ID is required.")]
    [Display(Name = "Apple Team ID")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "Apple Team ID must be 10 characters.")]
    public string AppleTeamId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Apple Issuer ID is required.")]
    [Display(Name = "Apple Issuer ID")]
    [StringLength(100)]
    public string AppleIssuerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Apple Key ID is required.")]
    [Display(Name = "Apple Key ID")]
    [StringLength(100)]
    public string AppleKeyId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? AppleAuthKeyPath { get; set; }

    [Required(ErrorMessage = "Domain URL is required.")]
    [Display(Name = "Domain")]
    [StringLength(500)]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string DomainUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sender ID is required.")]
    [Display(Name = "Sender ID")]
    [StringLength(100)]
    public string OneSignalSenderId { get; set; } = string.Empty;

    [Required(ErrorMessage = "App ID is required.")]
    [Display(Name = "App ID")]
    [StringLength(100)]
    public string OneSignalAppId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rest API Key is required.")]
    [Display(Name = "Rest API Key")]
    [StringLength(500)]
    [DataType(DataType.Password)]
    public string OneSignalRestApiKey { get; set; } = string.Empty;

    [StringLength(500)]
    public string? FirebaseIosConfigPath { get; set; }

    [StringLength(500)]
    public string? FirebaseAndroidConfigPath { get; set; }

    /// <summary>
    /// Maps a persisted deployment entity to a form view model for display.
    /// </summary>
    /// <param name="entity">Deployment loaded from the database.</param>
    /// <returns>A view model safe to render in Create/Edit forms.</returns>
    public static AppDeploymentFormViewModel FromEntity(AppDeployment entity)
    {
        return new AppDeploymentFormViewModel
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
            OneSignalRestApiKey = string.Empty,
            FirebaseIosConfigPath = entity.FirebaseIosConfigPath,
            FirebaseAndroidConfigPath = entity.FirebaseAndroidConfigPath
        };
    }

    /// <summary>
    /// Creates a blank form view model with the app name prefilled from token issuance.
    /// </summary>
    /// <param name="clientAppName">Client app name from the form-access token.</param>
    /// <returns>A new view model ready for the Create form.</returns>
    public static AppDeploymentFormViewModel ForNewDeployment(string clientAppName)
    {
        return new AppDeploymentFormViewModel
        {
            AppName = TruncateForAppName(clientAppName)
        };
    }

    /// <summary>
    /// Caps prefilled App Name to the model's max length so validation does not fail on first render.
    /// </summary>
    /// <param name="clientAppName">Raw client app name from token issuance.</param>
    /// <returns>Trimmed app name within the 30-character limit.</returns>
    public static string TruncateForAppName(string clientAppName)
    {
        const int maxLength = 30;
        string trimmed = clientAppName.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    /// <summary>
    /// Maps this view model onto a domain entity, optionally preserving existing asset paths and secrets.
    /// </summary>
    /// <param name="existing">Existing deployment row when editing; null on create.</param>
    /// <returns>A domain entity ready for persistence.</returns>
    public AppDeployment MapToEntity(AppDeployment? existing = null)
    {
        var entity = new AppDeployment
        {
            Id = Id,
            CommonName = CommonName,
            OrganizationUnit = OrganizationUnit,
            OrganizationName = OrganizationName,
            LocalityName = LocalityName,
            StateName = StateName,
            Country = Country,
            AdminEmail = AdminEmail,
            AppName = AppName,
            ShortDescription = ShortDescription,
            FullDescription = FullDescription,
            AppleName = AppleName,
            AppleSubtitle = AppleSubtitle,
            ApplePromotionalText = ApplePromotionalText,
            AppleDescription = AppleDescription,
            AppleKeywords = AppleKeywords,
            AppleSupportUrl = AppleSupportUrl,
            AppleMarketingUrl = AppleMarketingUrl,
            AppleCopyright = AppleCopyright,
            ContactFirstName = ContactFirstName,
            ContactLastName = ContactLastName,
            ContactPhoneNumber = ContactPhoneNumber,
            ContactEmailAddress = ContactEmailAddress,
            PrivacyPolicyUrl = PrivacyPolicyUrl,
            MobileAppIconPath = MobileAppIconPath,
            LaunchImagePath = LaunchImagePath,
            StoreIconPath = StoreIconPath,
            FeatureGraphicPath = FeatureGraphicPath,
            AndroidPackageName = AndroidPackageName,
            GooglePlayListingUrl = GooglePlayListingUrl,
            PlayStoreKeyPath = PlayStoreKeyPath,
            IosBundleId = IosBundleId,
            AppleTeamId = AppleTeamId,
            AppleIssuerId = AppleIssuerId,
            AppleKeyId = AppleKeyId,
            AppleAuthKeyPath = AppleAuthKeyPath,
            DomainUrl = DomainUrl,
            OneSignalSenderId = OneSignalSenderId,
            OneSignalAppId = OneSignalAppId,
            OneSignalRestApiKey = OneSignalRestApiKey,
            FirebaseIosConfigPath = FirebaseIosConfigPath,
            FirebaseAndroidConfigPath = FirebaseAndroidConfigPath
        };

        if (existing is not null)
        {
            entity.MobileAppIconPath ??= existing.MobileAppIconPath;
            entity.LaunchImagePath ??= existing.LaunchImagePath;
            entity.StoreIconPath ??= existing.StoreIconPath;
            entity.FeatureGraphicPath ??= existing.FeatureGraphicPath;
            entity.FirebaseIosConfigPath ??= existing.FirebaseIosConfigPath;
            entity.FirebaseAndroidConfigPath ??= existing.FirebaseAndroidConfigPath;
            entity.PlayStoreKeyPath ??= existing.PlayStoreKeyPath;
            entity.AppleAuthKeyPath ??= existing.AppleAuthKeyPath;

            if (string.IsNullOrWhiteSpace(entity.OneSignalRestApiKey))
            {
                entity.OneSignalRestApiKey = existing.OneSignalRestApiKey;
            }
        }

        return entity;
    }
}
