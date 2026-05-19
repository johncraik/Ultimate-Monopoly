using JC.Core.Helpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.TagHelpers;

public enum ProfileCircleSize
{
    Sm,
    Md,
    Lg
}

[HtmlTargetElement("profile-circle", TagStructure = TagStructure.WithoutEndTag)]
public class ProfileCircleTagHelper : TagHelper
{
    private const string DefaultBgColour = "#111111";

    public UserProfileViewModel? User { get; set; }

    public ProfileCircleSize Size { get; set; } = ProfileCircleSize.Md;

    public bool Preview { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (User is null)
        {
            output.SuppressOutput();
            return;
        }

        var bg = string.IsNullOrEmpty(User.AvatarColour) ? DefaultBgColour : User.AvatarColour;
        var fg = ColourHelper.FontColour(bg);
        var (rem, fontClass) = SizeToCss(Size);

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;

        output.Attributes.SetAttribute("class",
            $"rounded-circle d-inline-flex align-items-center justify-content-center overflow-hidden flex-shrink-0 fw-semibold {fontClass}");
        output.Attributes.SetAttribute("style",
            $"background-color: {bg}; color: {fg}; width: {rem}rem; height: {rem}rem;");

        if (Preview)
            output.Attributes.SetAttribute("data-avatar-preview", string.Empty);

        if (!string.IsNullOrEmpty(User.AvatarImageUrl))
        {
            var alt = string.IsNullOrEmpty(User.DisplayName) ? "Avatar" : User.DisplayName;
            output.Content.SetHtmlContent(
                $"<img src=\"{User.AvatarImageUrl}\" alt=\"{System.Net.WebUtility.HtmlEncode(alt)}\" class=\"mw-100 mh-100 p-1\" />");
        }
        else
        {
            output.Content.SetContent(User.Initial);
        }
    }

    private static (string Rem, string FontClass) SizeToCss(ProfileCircleSize size) => size switch
    {
        ProfileCircleSize.Sm => ("2",    "small"),
        ProfileCircleSize.Lg => ("5",    "fs-2"),
        _                    => ("2.75", "")
    };
}
