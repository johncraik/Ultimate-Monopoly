using JC.Core.Models;
using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Services.BoardSkins;

namespace UltimateMonopoly.Pages.Boards;

[Authorize]
public class EditModel : PageModel
{
    private readonly BoardSkinService _boardSkins;
    private readonly IUserInfo _userInfo;

    public EditModel(BoardSkinService boardSkins,
        IUserInfo userInfo)
    {
        _boardSkins = boardSkins;
        _userInfo = userInfo;
    }

    public string? Id { get; private set; }
    public BoardSkinViewModel? Skin { get; private set; }
    public List<SpaceSection> Sections { get; private set; } = [];

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        Id = id;

        // Creating a new skin — only the details card is shown until it has been saved.
        if (string.IsNullOrWhiteSpace(id))
        {
            if(_userInfo.IsInRole(AppRoles.Restricted))
                return Unauthorized();
            
            return Page();
        }

        Skin = await _boardSkins.GetBoardSkin(id);
        if (Skin is null) return NotFound();

        // Always build sections from the default board so DefaultName is the
        // default space name. The custom board would substitute custom names.
        var board = await _boardSkins.GetBoard(null)
            ?? throw new InvalidOperationException("Default board not available");

        var customs = Skin.Spaces.ToDictionary(s => s.Index, s => s);

        Sections = BuildSections(board, customs);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id, string? name, string? description, string? action)
    {
        var modelState = new ModelStateWrapper(ModelState, ignorePrefix: true);
        var result = await _boardSkins.TrySaveSkin(id, name, description, modelState);

        if (!result.Success)
        {
            StatusMessage = FirstError() ?? "Could not save board skin.";
            StatusKind = "danger";
            return RedirectToPage(new { id });
        }

        StatusMessage = string.IsNullOrWhiteSpace(id) ? "Board skin created." : "Board skin updated.";
        StatusKind = "success";

        return string.Equals(action, "close", StringComparison.OrdinalIgnoreCase)
            ? RedirectToPage("./Index")
            : RedirectToPage(new { id = result.Id });
    }

    public async Task<IActionResult> OnPostSaveSpaceAsync(string? id, ushort index, string? spaceId, string? name)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            StatusMessage = "Save the board skin before customising spaces.";
            StatusKind = "danger";
            return RedirectToPage(new { id });
        }

        var modelState = new ModelStateWrapper(ModelState, ignorePrefix: true);
        var ok = await _boardSkins.TrySaveSpace(id, spaceId, index, name, modelState);

        StatusMessage = ok
            ? (string.IsNullOrWhiteSpace(spaceId) ? "Custom space created." : "Custom space updated.")
            : (FirstError() ?? "Could not save the space.");
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteSpaceAsync(string? id, string? spaceId)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(spaceId))
        {
            StatusMessage = "Missing skin or space reference.";
            StatusKind = "danger";
            return RedirectToPage(new { id });
        }

        var ok = await _boardSkins.TryDeleteSpace(id, spaceId);
        StatusMessage = ok ? "Customisation removed." : "Could not remove customisation.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { id });
    }

    private string? FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;

    private static List<SpaceSection> BuildSections(Board board, IReadOnlyDictionary<ushort, BoardSkinSpaceViewModel> customs)
    {
        SpaceCard ToCard(BoardSpace s) =>
            new(s.Index, s.Name, customs.GetValueOrDefault(s.Index));

        List<SpaceCard> Filter(Func<BoardSpace, bool> pred) =>
            board.Spaces.Where(pred).OrderBy(s => s.Index).Select(ToCard).ToList();

        return
        [
            new("brown",     "Brown",            "bg-prop-brown",     SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Brown)),
            new("blue",      "Blue",             "bg-prop-blue",      SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Blue)),
            new("pink",      "Pink",             "bg-prop-pink",      SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Pink)),
            new("orange",    "Orange",           "bg-prop-orange",    SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Orange)),
            new("red",       "Red",              "bg-prop-red",       SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Red)),
            new("yellow",    "Yellow",           "bg-prop-yellow",    SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Yellow)),
            new("green",     "Green",            "bg-prop-green",     SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Green)),
            new("dark-blue", "Dark Blue",        "bg-prop-dark-blue", SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.DarkBlue)),
            new("stations",  "Stations",         "bg-prop-station",   SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Station)),
            new("utilities", "Utilities",        "bg-prop-utility",   SpaceShape.Rect,   Filter(s => s.PropertySet == PropertySet.Utility)),
            new("corners",   "Corners and Jail", null,                SpaceShape.Square, Filter(s => s.Index.IsCorner())),
            new("tax",       "Tax",              null,                SpaceShape.Rect,   Filter(s => s.SpaceType == BoardSpaceType.Tax)),
        ];
    }
}
