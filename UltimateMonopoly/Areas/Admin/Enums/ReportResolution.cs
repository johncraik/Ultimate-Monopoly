namespace UltimateMonopoly.Areas.Admin.Enums;

[Flags]
public enum ReportResolution
{
    AnyAction = -1,
    Open = 0,
    Handled = 1,
    AccountRestricted = 2,
    AccountDisabled = 4,
    AccountDeleted = 8,
    
    
    FullAction = AccountRestricted | AccountDisabled,
    AllActions = AccountRestricted | AccountDisabled | AccountDeleted
}