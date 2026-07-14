namespace MobileAppDeployment.Helpers;

public static class FieldHelpTexts
{
    public record Entry(string Title, string Body);

    public static IReadOnlyDictionary<string, Entry> All { get; } = new Dictionary<string, Entry>
    {
        ["CommonName"] = new(
            "Common Name (CN)",
            "The Common Name is the primary hostname your SSL certificate will secure. Use your apex domain (example.com) or a subdomain (www.example.com). This value must match the domain users visit in the browser and is embedded in the certificate used for HTTPS."),
        ["OrganizationUnit"] = new(
            "Organization Unit (OU)",
            "Identifies the department or team within your organization responsible for this certificate. Common examples include IT, Engineering, or Mobile Apps. Certificate authorities use this as part of the distinguished name on the certificate."),
        ["OrganizationName"] = new(
            "Organization Name (O)",
            "The legal or registered name of your company or organization as it should appear on the certificate. This should match official business records where possible."),
        ["LocalityName"] = new(
            "Locality / City (L)",
            "The city or town where your organization is located. Spell out the full city name rather than using abbreviations."),
        ["StateName"] = new(
            "State / Province (S)",
            "The state, province, or region where your organization is registered. Use the full name (for example, California rather than CA) as required by most certificate authorities."),
        ["Country"] = new(
            "Country (C)",
            "A two-letter ISO 3166 country code for your organization's location, such as US, GB, or IN. Do not include punctuation or use three-letter codes."),
        ["AdminEmail"] = new(
            "Admin email",
            "The administrative contact email for certificate-related notifications and domain verification. Use an address monitored by your operations or IT team."),
        ["AppName"] = new(
            "App name",
            "The public name displayed on device home screens and in store listings. Keep it concise and recognizable. Both Apple App Store and Google Play enforce a 30-character limit for the primary listing name."),
        ["ShortDescription"] = new(
            "Short description",
            "A brief summary shown in store search results and listing previews. Google Play uses this as the short description (max 80 characters). Write a clear value proposition that encourages installs."),
        ["FullDescription"] = new(
            "Full description",
            "The complete store listing description explaining features, benefits, and use cases. You can use multiple paragraphs. Google Play allows up to 4,000 characters. Avoid misleading claims and follow each store's content policies."),
        ["AndroidPackageName"] = new(
            "Package name",
            "The unique application ID for your Android app, written in reverse-DNS format (com.company.app). It must match the applicationId in your Android project and the listing in Google Play Console. Once published, this identifier cannot be changed."),
        ["GooglePlayListingUrl"] = new(
            "Google Play listing URL",
            "The full URL of your existing Google Play store listing, if the app is already published. If you are submitting a new app, leave this blank and the deployment pipeline will create a new listing using the package name you provide."),
        ["PlayStoreKeyFile"] = new(
            "Play Store key (JSON)",
            "Upload the Google Play service-account JSON key used by automation to call Google Play Developer APIs. Generate it from Google Cloud IAM, grant required Play Console permissions, and keep it private."),
        ["IosBundleId"] = new(
            "Bundle ID",
            "The unique identifier for your iOS app registered in Apple Developer and App Store Connect. It must exactly match the bundle identifier in your Xcode project and provisioning profiles. Bundle IDs use reverse-DNS notation, such as com.company.app."),
        ["AppleTeamId"] = new(
            "Apple Team ID",
            "Your 10-character Apple Developer Program Team ID, found on the Membership Details page in your Apple Developer account. This is required to sign iOS builds and associate them with the correct developer team."),
        ["AppleIssuerId"] = new(
            "Apple Issuer ID",
            "Issuer ID from App Store Connect API Keys. This identifies your App Store Connect API provider account when using key-based authentication."),
        ["AppleKeyId"] = new(
            "Apple Key ID",
            "Key ID of the App Store Connect API key. This ID is paired with Issuer ID and the .p8 private key during iOS automation."),
        ["AppleAuthKeyFile"] = new(
            "Apple Auth Key (.p8)",
            "Upload the App Store Connect API private key file (.p8) downloaded once from Apple. Keep this file secure and rotate it if exposed."),
        ["DomainUrl"] = new(
            "Domain",
            "The production URL where your web application will be hosted, including https://. Before deployment, point your domain's DNS A record to the deployment server. A free SSL certificate will be provisioned automatically via Let's Encrypt once DNS propagation completes."),
        ["AppleName"] = new(
            "Apple listing name",
            "The name shown on your App Store product page. Apple allows up to 30 characters. This can differ slightly from your general app name but should remain consistent with your brand."),
        ["AppleSubtitle"] = new(
            "Subtitle",
            "A short tagline displayed below the app name on the App Store. Maximum 30 characters. Use it to highlight your app's primary benefit or category."),
        ["ApplePromotionalText"] = new(
            "Promotional text",
            "Optional marketing copy that appears above the description on the App Store. You can update it without submitting a new app version. Maximum 170 characters. Useful for time-sensitive announcements or seasonal messaging."),
        ["AppleDescription"] = new(
            "Apple description",
            "The full App Store description for iOS users. Explain key features, workflows, and differentiators. Apple allows up to 4,000 characters. Follow App Store Review Guidelines regarding accuracy and prohibited content."),
        ["AppleKeywords"] = new(
            "Keywords",
            "Comma-separated search terms that help users discover your app on the App Store. Maximum 100 characters total. Do not repeat your app name, use competitor names, or include irrelevant terms."),
        ["AppleSupportUrl"] = new(
            "Support URL",
            "A web page where users can get help with your app, such as a support portal, FAQ, or contact page. Apple may review this URL during app review. Optional but recommended for better user trust."),
        ["AppleMarketingUrl"] = new(
            "Marketing URL",
            "An optional marketing or product landing page for your app. This appears on your App Store listing and can showcase screenshots, videos, or additional product information beyond the store page."),
        ["AppleCopyright"] = new(
            "Copyright",
            "The copyright line displayed on your App Store listing, typically in the format © 2026 Company Name. Use your legal entity name and the current year."),
        ["OneSignalSenderId"] = new(
            "Sender ID",
            "The Firebase Cloud Messaging (FCM) Sender ID associated with your OneSignal app. Find it in the OneSignal dashboard under Settings → Platforms → Google Android, or in your Firebase project settings."),
        ["OneSignalAppId"] = new(
            "OneSignal App ID",
            "The unique application identifier for your OneSignal project. Copy it from Settings → Keys & IDs in the OneSignal dashboard. This links push notifications to the correct OneSignal app."),
        ["OneSignalRestApiKey"] = new(
            "Rest API Key",
            "The REST API key used to send push notifications and manage subscribers programmatically. Find it in OneSignal under Settings → Keys & IDs. Treat this as a secret — it grants API access to your notification service."),
        ["firebaseIosConfigFile"] = new(
            "GoogleService-Info.plist",
            "The Firebase configuration file for iOS, downloaded from the Firebase console (Project settings → Your apps → iOS). It contains API keys and project identifiers required for Firebase services such as analytics and push notifications on iOS."),
        ["firebaseAndroidConfigFile"] = new(
            "google-services.json",
            "The Firebase configuration file for Android, downloaded from the Firebase console (Project settings → Your apps → Android). Place the file in your Android app module. It links your app to your Firebase project for messaging, analytics, and other services."),
        ["mobileAppIconFile"] = new(
            "Mobile app icon",
            "The app icon used across store listings and generated device sizes. Upload a PNG or JPEG, up to 1 MB, exactly 512 × 512 pixels. Keep the logo centered with safe margins — corners may be masked on some devices."),
        ["launchImageFile"] = new(
            "Launch image",
            "The splash screen image shown while your app loads. Upload a 2732 × 2732 PNG. Keep important content within the central 1024 × 1024 safe zone, as edges may be cropped on different screen sizes and orientations."),
        ["storeIconFile"] = new(
            "Store icon",
            "The high-resolution icon displayed on your Google Play store listing. Must be 512 × 512 PNG with a solid, opaque background. Google Play does not accept transparent backgrounds for store icons."),
        ["featureGraphicFile"] = new(
            "Feature graphic / banner",
            "A promotional banner shown at the top of your Google Play listing. Must be a PNG or JPEG, up to 15 MB, and exactly 1024 × 500 pixels. Use it to showcase your brand, key features, or a compelling visual that represents the app."),
        ["ContactFirstName"] = new(
            "First name",
            "The first name of the person Apple and Google can contact regarding this app submission. Use a real contact who can respond to store review inquiries."),
        ["ContactLastName"] = new(
            "Last name",
            "The last name of your store contact person. This appears in developer account and app submission records."),
        ["ContactPhoneNumber"] = new(
            "Phone number",
            "A reachable phone number for store review teams or compliance inquiries. Include the country code where applicable."),
        ["ContactEmailAddress"] = new(
            "Contact email address",
            "The email address store platforms use for submission status, review questions, and policy notifications. Use an address monitored during business hours."),
        ["PrivacyPolicyUrl"] = new(
            "Privacy policy URL",
            "A publicly accessible URL to your app's privacy policy. Apple requires this and will reject submissions if the page is empty or inaccessible. The policy must describe what data you collect, how it is used, and how users can request deletion.")
    };

    public static bool TryGet(string key, out Entry entry) => All.TryGetValue(key, out entry!);
}
