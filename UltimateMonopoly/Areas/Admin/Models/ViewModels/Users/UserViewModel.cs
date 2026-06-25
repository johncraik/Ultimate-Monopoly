using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;

public class UserViewModel
{
    //Public facing info, including UserId
    public UserProfileViewModel Profile { get; }
    
    //Admin visible only properties
    public string Email { get; }
    public bool EmailConfirmed { get; }
    public string PhoneNumber { get; }
    
    public bool TwoFactorEnabled { get; }
    public int AccessFailedCount { get; }
    public bool LockoutEnabled { get; }
    public string LockoutEnd { get; }
    public bool IsEnabled { get; }
    
    public string LastLogin { get; }
    public string LastActive { get; }

    public bool? IsRestricted { get; }
    public IReadOnlyList<string> Roles { get; } = [];
    
    public UserViewModel(AppUser user, bool? isRestricted = null, List<string>? roles = null)
    {
        //Dont need to load profile circle img/colour for user management
        Profile = new UserProfileViewModel(user, null);
        
        Email = user.Email ?? "None";
        EmailConfirmed = user.EmailConfirmed;
        PhoneNumber = user.PhoneNumber ?? "None";
        TwoFactorEnabled = user.TwoFactorEnabled;
        
        AccessFailedCount = user.AccessFailedCount;
        LockoutEnabled = user.LockoutEnabled;
        LockoutEnd = user.LockoutEnd?.ToLocalTime().ToString("g") ?? "N/A";
        IsEnabled = user.IsEnabled;
        
        LastLogin = user.LastLoginUtc?.ToLocalTime().ToString("g") ?? "Never";
        LastActive = user.LastActiveUtc?.ToLocalTime().ToString("g") ?? "Never";

        IsRestricted = isRestricted;
        if (roles != null)
            Roles = roles.AsReadOnly();
    }

    public UserViewModel()
    {
        Profile = new UserProfileViewModel(0, 0, 0, true);
        Email = "None";
        EmailConfirmed = false;
        PhoneNumber = "None";
        TwoFactorEnabled = false;
        
        AccessFailedCount = 0;
        LockoutEnabled = false;
        LockoutEnd = "N/A";
        IsEnabled = false;
        
        LastLogin = "Never";
        LastActive = "Never";
        
        IsRestricted = null;
        Roles = [];
    }
}