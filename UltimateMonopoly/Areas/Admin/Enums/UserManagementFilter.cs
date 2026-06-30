namespace UltimateMonopoly.Areas.Admin.Enums;

[Flags]
public enum UserManagementFilter
{
    None = 0,
    NotRestricted = 1 << 0, //Has no restricted role
    Restricted = 1 << 1,    //Has restricted role
    Enabled = 1 << 2,       //IsEnabled = true
    Disabled = 1 << 3       //IsEnabled = false
}