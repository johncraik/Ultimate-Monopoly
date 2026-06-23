using JC.Core.Models.Pagination;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

public class UserTableModel
{
    public UserManagementFilter RestrictedFilter { get; } = UserManagementFilter.None;
    public UserManagementFilter DisabledFilter { get; } = UserManagementFilter.None;
    public string? Search { get; }
    public PagedList<UserViewModel> Users { get; }
    public bool FullTable { get; }

    public UserTableModel(PagedList<UserViewModel> users, string? search, 
        UserManagementFilter restrictedFilter, UserManagementFilter disabledFilter)
    {
        Users = users;
        RestrictedFilter = restrictedFilter;
        DisabledFilter = disabledFilter;
        Search = search;
        FullTable = true;
    }

    public UserTableModel(PagedList<UserViewModel> users, string? search = null)
    {
        Users = users;
        Search = search;
        FullTable = false;
    }
}