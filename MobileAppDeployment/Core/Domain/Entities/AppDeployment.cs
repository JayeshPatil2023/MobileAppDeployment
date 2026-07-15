using System.ComponentModel.DataAnnotations;

namespace MobileAppDeployment.Core.Domain.Entities;

/// <summary>
/// Mobile app store submission configuration for a single client app.
/// </summary>
/// <remarks>
/// Data annotations with <see cref="RequiredAttribute"/> remain on fields so the UI can show
/// asterisks and so <c>StartDeployment</c> can run full validation.
/// Create/Edit <strong>Save</strong> only enforces Organization Name and App Name
/// (see <c>AppDeploymentValidation.ApplySaveValidation</c>).
/// </remarks>
public class AppDeployment
{
    public int Id { get; set; }

    // Certificate / Organization Details
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

    // General App Details
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

    // Apple App Listing
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

    // Contact Information
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

    // Assets (stored file paths)
    [StringLength(500)]
    public string? MobileAppIconPath { get; set; }

    [StringLength(500)]
    public string? LaunchImagePath { get; set; }

    [StringLength(500)]
    public string? StoreIconPath { get; set; }

    [StringLength(500)]
    public string? FeatureGraphicPath { get; set; }

    // Mobile configuration
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

    // Website configuration
    [Required(ErrorMessage = "Domain URL is required.")]
    [Display(Name = "Domain")]
    [StringLength(500)]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string DomainUrl { get; set; } = string.Empty;

    // OneSignal
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

    /// <summary>
    /// Data Protection–encrypted OneSignal REST API key persisted at rest.
    /// </summary>
    [StringLength(2000)]
    public string? OneSignalRestApiKeyEncrypted { get; set; }

    // Firebase config file paths
    [StringLength(500)]
    public string? FirebaseIosConfigPath { get; set; }

    [StringLength(500)]
    public string? FirebaseAndroidConfigPath { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
