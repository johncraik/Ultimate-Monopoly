using System.ComponentModel.DataAnnotations;
using JC.Identity.Models;
using UltimateMonopoly.Attributes;

namespace UltimateMonopoly.Data;

public class AppUser : BaseUser
{
    public uint NumberOfWins { get; set; }
    public uint NumberOfLosses { get; set; }
    public uint NumberOfDraws { get; set; }

    [HexColour]
    [MaxLength(7)]
    public string? AvatarColour { get; private set; }

    [MaxLength(10240)]
    public string? AvatarImageName { get; set; }

    public DateTime? LastActiveUtc { get; set; }

    /// <summary>UTC timestamp the account was registered. Set once at registration; powers the dashboard's
    /// registration-trend and cohort metrics. Nullable so pre-existing accounts (before this column) read null.</summary>
    public DateTime? RegisteredUtc { get; set; }

    /// <summary>Set true once the player dismisses the first-run welcome on the hub. Default false; the hub also
    /// auto-hides the welcome once the player has any games, so this only matters for brand-new accounts.</summary>
    public bool HasDismissedWelcome { get; set; }

    public string? SetAvatarColour(string? colour)
    {
        if (string.IsNullOrWhiteSpace(colour))
        {
            AvatarColour = null;
            return null;
        }

        colour = colour.Trim();
        if (!colour.StartsWith('#'))
            colour = "#" + colour;

        if (!HexColourAttribute.IsHexColour(colour))
            throw new ArgumentException(
                "Value must be a valid hex colour (#RGB or #RRGGBB).",
                nameof(colour));

        AvatarColour = colour;
        return colour;
    }
}

public record UserProfile(string? AvatarColour, string? AvatarImageName);