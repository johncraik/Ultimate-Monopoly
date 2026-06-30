using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services.Dashboard;

/// <summary>
/// Builds the Games spoke dashboard (C1) and the hub's Games tile. <b>SystemAdmin-only</b> (game management is
/// SystemAdmin-gated). Lifecycle / state / outcome / board / config are cheap counts on <c>Game</c>; players-per-
/// game, game-length, abandonment and turn-throughput aggregate over <c>GamePlayer</c>/<c>GameTurn</c> in SQL
/// (one row per game, not per turn); storage uses <c>LEN()</c> projection so the JSON blobs are never transferred.
/// </summary>
public class GamesDashboardService
{
    private readonly AppDbContext _context;
    private readonly SettingsDictionary _settings;

    public GamesDashboardService(AppDbContext context, SettingsDictionary settings, IUserInfo userInfo)
    {
        _context = context;
        _settings = settings;
        if (!userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    // ---- helpers ----

    private static List<SeriesPoint> WeeklyPoints(DateTime now, List<DateTime> timestamps)
    {
        DateTime StartOfWeek(DateTime dt) => dt.Date.AddDays(-(((int)dt.DayOfWeek + 6) % 7)); // Monday-start
        var thisWeek = StartOfWeek(now);
        var points = new List<SeriesPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var ws = thisWeek.AddDays(-7 * i);
            var we = ws.AddDays(7);
            points.Add(new SeriesPoint(ws.ToString("dd MMM"), timestamps.Count(t => t >= ws && t < we)));
        }
        return points;
    }

    private static SeriesWidget WeeklySeries(DateTime now, List<DateTime> ts, string id, string title, string icon, string tone, string seriesType, string height) =>
        new() { Id = id, Title = title, Icon = icon, Tone = tone, SeriesType = seriesType, Height = height, Points = WeeklyPoints(now, ts) };

    private static string Size(long bytes)
    {
        var kb = bytes / 1024.0;
        return kb < 1024 ? $"{kb:0.#} KB" : $"{kb / 1024.0:0.##} MB";
    }

    private int AbandonWeeks() => _settings.Get().AbandonedRetentionWeeks > 0 ? _settings.Get().AbandonedRetentionWeeks : 6;

    // ---- hub tile ----

    public async Task<GamesDashboardTile> BuildTile()
    {
        var now = DateTime.UtcNow;
        var active = _context.Games.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive);

        var total = await active.CountAsync();
        var inPlay = await active.CountAsync(g => g.State == GameState.InPlay);
        var finished = await active.CountAsync(g => g.State == GameState.Finished);
        var cancelled = await active.CountAsync(g => g.State == GameState.Cancelled);
        var concluded = finished + cancelled;
        var completionRate = concluded == 0 ? 0 : Math.Round(100.0 * finished / concluded, 0);

        // Abandonment (tile approximation): in-play games whose latest turn is older than the window.
        var abandonCutoff = now.AddDays(-7 * AbandonWeeks());
        var inPlayIds = await active.Where(g => g.State == GameState.InPlay).Select(g => g.Id).ToListAsync();
        var lastTurns = inPlayIds.Count == 0
            ? new List<DateTime>()
            : await _context.GameTurns.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(t => inPlayIds.Contains(t.GameId))
                .GroupBy(t => t.GameId).Select(g => g.Max(t => t.CreatedUtc)).ToListAsync();
        var abandoned = lastTurns.Count(t => t < abandonCutoff);

        var created = await active.Where(g => g.CreatedUtc >= now.AddDays(-84)).Select(g => g.CreatedUtc).ToListAsync();
        var concludedTs = await active
            .Where(g => (g.State == GameState.Finished || g.State == GameState.Cancelled) && (g.LastModifiedUtc ?? g.CreatedUtc) >= now.AddDays(-84))
            .Select(g => g.LastModifiedUtc ?? g.CreatedUtc).ToListAsync();

        return new GamesDashboardTile
        {
            GamesCreated = WeeklySeries(now, created, "tile-games-created", "Created · 12 weeks", "bi-plus-circle", "primary", "SplineArea", "160px"),
            GamesConcluded = WeeklySeries(now, concludedTs, "tile-games-concluded", "Concluded · 12 weeks", "bi-check-circle", "success", "SplineArea", "160px"),
            Kpis = new[]
            {
                new MetricCard { Label = "Live now", Value = inPlay.ToString("N0"), Icon = "bi-broadcast", Tone = "success" },
                new MetricCard { Label = "Total games", Value = total.ToString("N0"), Icon = "bi-controller", Tone = "primary" },
                new MetricCard { Label = "Completion", Value = $"{completionRate:0}%", Icon = "bi-check2-circle", Tone = "info" }
            },
            Alerts = new[]
            {
                new AlertWidget { Label = "abandoned (stale in-play)", Count = abandoned, Tone = "warning", Icon = "bi-hourglass-bottom", Href = "/Admin/Games/Index?State=InPlay" }
            }
        };
    }

    // ---- full spoke ----

    public async Task<GamesDashboardModel> Build()
    {
        var now = DateTime.UtcNow;
        var weeks = AbandonWeeks();

        var games = await _context.Games.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(g => new { g.Id, g.Name, g.State, g.Outcome, g.RoundingRule, g.BoardId, g.CreatedUtc, g.LastModifiedUtc })
            .ToListAsync();

        var total = games.Count;
        var setup = games.Count(g => g.State == GameState.Setup);
        var inPlay = games.Count(g => g.State == GameState.InPlay);
        var finished = games.Count(g => g.State == GameState.Finished);
        var cancelled = games.Count(g => g.State == GameState.Cancelled);
        var concluded = finished + cancelled;
        var completionRate = concluded == 0 ? 0 : Math.Round(100.0 * finished / concluded, 1);
        var cancellationRate = concluded == 0 ? 0 : Math.Round(100.0 * cancelled / concluded, 1);
        var winners = games.Count(g => g.State == GameState.Finished && g.Outcome == GameOutcome.Winner);
        var draws = games.Count(g => g.State == GameState.Finished && g.Outcome == GameOutcome.Drawn);

        // Turn stats per game (one row per game).
        var turnStats = await _context.GameTurns.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .GroupBy(t => t.GameId)
            .Select(g => new { GameId = g.Key, MaxTurn = g.Max(t => t.TurnNumber), LastTurnUtc = g.Max(t => t.CreatedUtc) })
            .ToListAsync();
        var turnsByGame = turnStats.ToDictionary(x => x.GameId, x => x);

        // Players per game.
        var playerCounts = await _context.GamePlayers.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .GroupBy(p => p.GameId).Select(g => new { GameId = g.Key, Count = g.Count() }).ToListAsync();
        var pcValues = playerCounts.Select(x => x.Count).ToList();
        var avgPlayers = pcValues.Count > 0 ? pcValues.Average() : 0;
        var playerBuckets = Enumerable.Range(2, 7)
            .Select(n => new HistogramBucket(n.ToString(), pcValues.Count(c => c == n))).ToList();

        // Game length (finished games) by max turn.
        var finishedLengths = games.Where(g => g.State == GameState.Finished && turnsByGame.ContainsKey(g.Id))
            .Select(g => (int)turnsByGame[g.Id].MaxTurn).ToList();
        var avgTurns = finishedLengths.Count > 0 ? finishedLengths.Average() : 0;
        // Bins tuned to typical play (games normally run 100+ turns): very short ≤30, short ≤80,
        // quick ≤120, standard ≤160, long 160+.
        var lengthBuckets = new List<HistogramBucket>
        {
            new("0–30", finishedLengths.Count(l => l <= 30)),
            new("31–80", finishedLengths.Count(l => l > 30 && l <= 80)),
            new("81–120", finishedLengths.Count(l => l > 80 && l <= 120)),
            new("121–160", finishedLengths.Count(l => l > 120 && l <= 160)),
            new("160+", finishedLengths.Count(l => l > 160))
        };
        var longest = games.Where(g => turnsByGame.ContainsKey(g.Id))
            .Select(g => new { g.Name, g.Id, Turns = (int)turnsByGame[g.Id].MaxTurn })
            .OrderByDescending(x => x.Turns).Take(5)
            .Select(x => new TopListRow(x.Name, $"{x.Turns:N0} turns", null, $"/Admin/Games/Details/{x.Id}")).ToList();

        var veryShort = games.Count(g => (g.State == GameState.Finished || g.State == GameState.Cancelled)
            && turnsByGame.ContainsKey(g.Id) && turnsByGame[g.Id].MaxTurn < 5);

        var abandonCutoff = now.AddDays(-7 * weeks);
        var abandoned = games.Count(g => g.State == GameState.InPlay &&
            (turnsByGame.TryGetValue(g.Id, out var ts) ? ts.LastTurnUtc < abandonCutoff : g.CreatedUtc < abandonCutoff));

        // Rounding-rule popularity.
        var roundingBuckets = games.GroupBy(g => g.RoundingRule).OrderBy(g => g.Key)
            .Select(g => new HistogramBucket(g.Key.ToDisplayName(), g.Count())).ToList();

        // Board usage.
        var defaultGames = games.Count(g => string.IsNullOrEmpty(g.BoardId));
        var customGames = total - defaultGames;
        var boardSegments = new List<BreakdownSegment>
        {
            new("Default board", defaultGames, DashboardPalette.Hex("secondary")),
            new("Custom boards", customGames, DashboardPalette.Hex("primary"))
        };
        var boardCounts = games.Where(g => !string.IsNullOrEmpty(g.BoardId))
            .GroupBy(g => g.BoardId!).Select(g => new { BoardId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var boardIds = boardCounts.Select(x => x.BoardId).ToList();
        var boardNames = await _context.BoardSkins.AsNoTracking().Where(b => boardIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
        var topBoards = boardCounts.Select(x => new TopListRow(
            boardNames.GetValueOrDefault(x.BoardId, "Unknown board"), $"{x.Count:N0} games")).ToList();

        // Storage (LEN projection — blobs never transferred).
        var snapBytes = await _context.GameSnapshots.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive).SumAsync(s => (long)s.StateJson.Length);
        var evBytes = await _context.GameTurnEvents.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive).SumAsync(e => (long)e.EventsJson.Length);
        var snapByGame = await _context.GameSnapshots.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .GroupBy(s => s.GameId).Select(g => new { GameId = g.Key, Bytes = g.Sum(s => (long)s.StateJson.Length) }).ToListAsync();
        var evByGame = await _context.GameTurnEvents.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .GroupBy(e => e.GameId).Select(g => new { GameId = g.Key, Bytes = g.Sum(e => (long)e.EventsJson.Length) }).ToListAsync();
        var sizeByGame = new Dictionary<string, long>();
        foreach (var s in snapByGame) sizeByGame[s.GameId] = s.Bytes;
        foreach (var e in evByGame) sizeByGame[e.GameId] = sizeByGame.GetValueOrDefault(e.GameId) + e.Bytes;
        var gameNameById = games.ToDictionary(g => g.Id, g => g.Name);
        var topSize = sizeByGame.OrderByDescending(kv => kv.Value).Take(5)
            .Select(kv => new TopListRow(gameNameById.GetValueOrDefault(kv.Key, "Unknown"), Size(kv.Value), null, $"/Admin/Games/Details/{kv.Key}")).ToList();

        // Trends.
        var turnTs = await _context.GameTurns.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(t => t.CreatedUtc >= now.AddDays(-84)).Select(t => t.CreatedUtc).ToListAsync();
        var concludedTs = games.Where(g => g.State == GameState.Finished || g.State == GameState.Cancelled)
            .Select(g => g.LastModifiedUtc ?? g.CreatedUtc).ToList();

        return new GamesDashboardModel
        {
            Alerts = new[]
            {
                new AlertWidget { Label = "abandoned (stale in-play)", Count = abandoned, Tone = "warning", Icon = "bi-hourglass-bottom", Href = "/Admin/Games/Index?State=InPlay" },
                new AlertWidget { Label = "very short (<5 turns)", Count = veryShort, Tone = "secondary", Icon = "bi-fast-forward" }
            },
            Kpis = new[]
            {
                new MetricCard { Label = "Total games", Value = total.ToString("N0"), Icon = "bi-controller", Tone = "primary" },
                new MetricCard { Label = "Live now", Value = inPlay.ToString("N0"), Icon = "bi-broadcast", Tone = "success" },
                new MetricCard { Label = "Awaiting players", Value = setup.ToString("N0"), Icon = "bi-hourglass-split", Tone = "info" },
                new MetricCard { Label = "Finished", Value = finished.ToString("N0"), Icon = "bi-trophy", Tone = "secondary" },
                new MetricCard { Label = "Cancelled", Value = cancelled.ToString("N0"), Icon = "bi-x-circle", Tone = "secondary" }
            },

            GamesByState = new BreakdownWidget
            {
                Id = "games-state", Title = "Games by state", Icon = "bi-diagram-3", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment>
                {
                    new("Setup", setup, DashboardPalette.Hex("info")),
                    new("In play", inPlay, DashboardPalette.Hex("success")),
                    new("Finished", finished, DashboardPalette.Hex("primary")),
                    new("Cancelled", cancelled, DashboardPalette.Hex("danger"))
                }
            },
            CompletionRate = new GaugeWidget { Id = "games-completion", Label = "Completion rate", Percent = completionRate, Tone = "success", Caption = $"{finished:N0} of {concluded:N0} concluded", Icon = "bi-check2-circle" },
            CancellationRate = new GaugeWidget { Id = "games-cancellation", Label = "Cancellation rate", Percent = cancellationRate, Tone = "danger", Caption = $"{cancelled:N0} of {concluded:N0} concluded", Icon = "bi-x-octagon" },
            OutcomeSplit = new BreakdownWidget
            {
                Id = "games-outcome", Title = "Outcome (finished games)", Icon = "bi-trophy", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment> { new("Winner", winners, DashboardPalette.Hex("success")), new("Drawn", draws, DashboardPalette.Hex("info")) },
                EmptyText = "No finished games yet."
            },

            PlayersPerGame = new HistogramWidget { Id = "games-players", Title = "Players per game", Icon = "bi-people", Tone = "primary", Buckets = playerBuckets },
            AvgPlayers = new MetricCard { Label = "Avg players / game", Value = avgPlayers.ToString("0.#"), Icon = "bi-people", Tone = "primary" },
            GameLength = new HistogramWidget { Id = "games-length", Title = "Game length (turns, finished)", Icon = "bi-list-ol", Tone = "info", Buckets = lengthBuckets },
            AvgTurns = new MetricCard { Label = "Avg turns / game", Value = avgTurns.ToString("0.#"), Icon = "bi-list-ol", Tone = "info" },
            LongestGames = new TopListWidget { Title = "Longest games", Icon = "bi-trophy", Rows = longest, EmptyText = "No games with turns yet." },

            RoundingRules = new HistogramWidget { Id = "games-rounding", Title = "Rounding rule", Icon = "bi-calculator", Tone = "secondary", Buckets = roundingBuckets },
            BoardUsage = new BreakdownWidget { Id = "games-boards", Title = "Board usage", Icon = "bi-grid-3x3", Style = BreakdownStyle.Donut, Segments = boardSegments },
            TopBoards = new TopListWidget { Title = "Top custom boards", Icon = "bi-grid", Rows = topBoards, EmptyText = "No custom boards in use." },

            TotalStorage = new MetricCard { Label = "Snapshot + event storage", Value = Size(snapBytes + evBytes), Icon = "bi-hdd", Tone = "secondary", Sub = $"{Size(snapBytes)} snapshots · {Size(evBytes)} events" },
            TopGamesBySize = new TopListWidget { Title = "Largest games by storage", Icon = "bi-hdd-stack", Rows = topSize, EmptyText = "No stored game history." },

            GamesCreated = WeeklySeries(now, games.Select(g => g.CreatedUtc).ToList(), "games-created", "Games created · 12 weeks", "bi-plus-circle", "primary", "SplineArea", "240px"),
            GamesConcluded = WeeklySeries(now, concludedTs, "games-concluded", "Games concluded · 12 weeks", "bi-check-circle", "success", "SplineArea", "240px"),
            TurnThroughput = WeeklySeries(now, turnTs, "games-throughput", "Turn throughput · 12 weeks", "bi-dice-5", "info", "Column", "240px")
        };
    }
}
