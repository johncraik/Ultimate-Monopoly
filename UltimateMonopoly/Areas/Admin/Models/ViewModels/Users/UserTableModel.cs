using JC.Core.Models.Pagination;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;

public class UserTableModel
{
    public UserManagementFilter RestrictedFilter { get; } = UserManagementFilter.None;
    public UserManagementFilter DisabledFilter { get; } = UserManagementFilter.None;
    public string? Search { get; }
    public PagedList<UserViewModel> Users { get; }
    public bool FullTable { get; }

    /// <summary>Where a row links in the non-full (picker) variant. Null → the default User Trail page;
    /// set it to reuse the picker for another destination (e.g. the notifications-by-user page). The row
    /// navigates to <c>{RowLinkBase}/{userId}</c>.</summary>
    public string? RowLinkBase { get; }

    public UserTableModel(PagedList<UserViewModel> users, string? search,
        UserManagementFilter restrictedFilter, UserManagementFilter disabledFilter)
    {
        Users = users;
        RestrictedFilter = restrictedFilter;
        DisabledFilter = disabledFilter;
        Search = search;
        FullTable = true;
    }

    public UserTableModel(PagedList<UserViewModel> users, string? search = null, string? rowLinkBase = null)
    {
        Users = users;
        Search = search;
        RowLinkBase = rowLinkBase;
        FullTable = false;
    }
}