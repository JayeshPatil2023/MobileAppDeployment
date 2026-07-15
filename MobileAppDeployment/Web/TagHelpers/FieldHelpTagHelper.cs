using Microsoft.AspNetCore.Razor.TagHelpers;
using MobileAppDeployment.Web.Helpers;

namespace MobileAppDeployment.Web.TagHelpers;

/// <summary>
/// Renders a field-help button that opens the shared help modal for a known help key.
/// </summary>
[HtmlTargetElement("field-help")]
public class FieldHelpTagHelper : TagHelper
{
    public required string Key { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!FieldHelpTexts.TryGet(Key, out var entry))
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", "field-help-btn");
        output.Attributes.SetAttribute("data-field-help", Key);
        output.Attributes.SetAttribute("aria-label", $"Help for {entry.Title}");
        output.Content.SetHtmlContent(
            """<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 015.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>""");
    }
}
