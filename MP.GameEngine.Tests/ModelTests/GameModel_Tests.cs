using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Snapshot;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.ModelTests;

public class GameModel_Tests
{
    // ─── Fixture ────────────────────────────────────────────────────────
    //
    // Four players. Dice numbers (unordered pairs) are unique per player:
    //
    //   A  OrderId 0   dice {2,5}   active
    //   B  OrderId 1   dice {3,6}   active
    //   C  OrderId 2   dice {5,1}   BANKRUPT
    //   D  OrderId 3   dice {4,6}   active
    //
    // Players are inserted into the list in a deliberately NON-seat order
    // ([D, B, A, C]) so that GetPlayers' OrderId sort is genuinely exercised.
    // This mirrors the real-world case from session notes 2026-05-28 S2 §9:
    // GameSetupService builds the list in EF/insertion order, which a seat
    // reorder during setup desyncs from OrderId. A fixture inserted in seat
    // order would mask the ordering invariant entirely.
    //
    // Property ownership:
    //   A — full Brown+Blue street (street partners), a station, plus a
    //       mortgaged and a reserved DarkBlue property.
    //   B — full Red+Yellow street with one BuiltOn property, plus a utility.
    //   D — full Pink+Orange street with one mortgaged property.
    //   Two Green spaces left unowned / in Free Parking.

    private const string A = "player-a";
    private const string B = "player-b";
    private const string C = "player-c";
    private const string D = "player-d";

    private static PlayerModel Player(string id, ushort orderId, ushort d1, ushort d2, bool bankrupt = false)
        => new()
        {
            PlayerId = id,
            OrderId = orderId,
            Dice1 = d1,
            Dice2 = d2,
            IsBankrupt = bankrupt,
            Money = 1500
        };

    private static PropertyModel Prop(string name, ushort index, string? owner, PropertyState state,
        StreetRuleQualifier qualifier = StreetRuleQualifier.None)
        => new()
        {
            Name = name,
            BoardIndex = index,
            OwnerPlayerId = owner,
            State = state,
            RentLevel = RentLevel.SINGLE,
            StreetRuleQualifier = qualifier
        };

    private static GameModel CreateGame(string currentPlayerId = A)
        => new()
        {
            GameId = "game-1",
            Metadata = new TurnMetadata
            {
                CurrentTurnId = "turn-1",
                CurrentPlayerId = currentPlayerId,
                TurnNumber = 1
            },
            Players =
            [
                Player(D, orderId: 3, d1: 4, d2: 6),
                Player(B, orderId: 1, d1: 3, d2: 6),
                Player(A, orderId: 0, d1: 2, d2: 5),
                Player(C, orderId: 2, d1: 5, d2: 1, bankrupt: true)
            ],
            Properties =
            [
                // A — Brown set (1, 3)
                Prop("Old Kent Road", 1, A, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Whitechapel Road", 3, A, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                // A — Blue set (6, 8, 9), street partner of Brown
                Prop("The Angel Islington", 6, A, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Euston Road", 8, A, PropertyState.Owned, StreetRuleQualifier.Qualified),
                Prop("Pentonville Road", 9, A, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                // A — a station, and DarkBlue mortgaged + reserved
                Prop("Kings Cross Station", 5, A, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Park Lane", 37, A, PropertyState.Mortgaged, StreetRuleQualifier.NeverBuiltOn),
                Prop("Mayfair", 39, A, PropertyState.Reserved, StreetRuleQualifier.NeverBuiltOn),

                // B — Red set (21, 23, 24) with one BuiltOn
                Prop("Strand", 21, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Fleet Street", 23, B, PropertyState.Owned, StreetRuleQualifier.BuiltOn),
                Prop("Trafalgar Square", 24, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                // B — Yellow set (26, 27, 29), street partner of Red
                Prop("Leicester Square", 26, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Coventry Street", 27, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Piccadilly", 29, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                // B — a utility
                Prop("Electric Company", 12, B, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),

                // D — Pink set (11, 13, 14) with one mortgaged
                Prop("Pall Mall", 11, D, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Whitehall", 13, D, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Northumberland Avenue", 14, D, PropertyState.Mortgaged, StreetRuleQualifier.NeverBuiltOn),
                // D — Orange set (16, 18, 19), street partner of Pink
                Prop("Bow Street", 16, D, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Marlborough Street", 18, D, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),
                Prop("Vine Street", 19, D, PropertyState.Owned, StreetRuleQualifier.NeverBuiltOn),

                // Unowned / Free Parking (Green set)
                Prop("Regent Street", 31, null, PropertyState.NotOwned),
                Prop("Oxford Street", 32, null, PropertyState.FreeParking)
            ]
        };

    /// <summary>
    /// All four players active, again inserted out of seat order ([C, A, D, B]).
    /// Used for the pure clockwise-rotation assertions without a bankrupt gap.
    /// </summary>
    private static GameModel CreateAllActiveGame(string currentPlayerId = A)
        => new()
        {
            GameId = "game-2",
            Metadata = new TurnMetadata
            {
                CurrentTurnId = "turn-1",
                CurrentPlayerId = currentPlayerId,
                TurnNumber = 1
            },
            Players =
            [
                Player(C, orderId: 2, d1: 6, d2: 1),
                Player(A, orderId: 0, d1: 2, d2: 5),
                Player(D, orderId: 3, d1: 2, d2: 6),
                Player(B, orderId: 1, d1: 3, d2: 4)
            ]
        };

    private static IEnumerable<string> Ids(IEnumerable<PlayerModel> players)
        => players.Select(p => p.PlayerId);


    // ─── GetPlayer ──────────────────────────────────────────────────────

    [Fact]
    public void GetPlayer_ActivePlayer_ReturnsPlayer()
    {
        var game = CreateGame();
        var player = game.GetPlayer(B);

        Assert.NotNull(player);
        Assert.Equal(B, player!.PlayerId);
    }

    [Fact]
    public void GetPlayer_ActivePlayerWithBankruptFilter_ReturnsNull()
    {
        // B is active, but we asked for the bankrupt-matching row.
        var game = CreateGame();
        Assert.Null(game.GetPlayer(B, bankrupt: true));
    }

    [Fact]
    public void GetPlayer_BankruptPlayerDefaultFilter_ReturnsNull()
    {
        // C is bankrupt; the default filter asks for an active row.
        var game = CreateGame();
        Assert.Null(game.GetPlayer(C));
    }

    [Fact]
    public void GetPlayer_BankruptPlayerWithBankruptFilter_ReturnsPlayer()
    {
        var game = CreateGame();
        var player = game.GetPlayer(C, bankrupt: true);

        Assert.NotNull(player);
        Assert.Equal(C, player!.PlayerId);
    }

    [Fact]
    public void GetPlayer_UnknownId_Throws()
    {
        var game = CreateGame();
        Assert.Throws<InvalidOperationException>(() => game.GetPlayer("ghost"));
    }


    // ─── CurrentPlayer ──────────────────────────────────────────────────

    [Fact]
    public void CurrentPlayer_ActiveCurrent_ReturnsCurrent()
    {
        var game = CreateGame(currentPlayerId: A);
        var player = game.CurrentPlayer();

        Assert.NotNull(player);
        Assert.Equal(A, player!.PlayerId);
    }

    [Fact]
    public void CurrentPlayer_ActiveCurrentWithBankruptFilter_ReturnsNull()
    {
        var game = CreateGame(currentPlayerId: A);
        Assert.Null(game.CurrentPlayer(bankrupt: true));
    }

    [Fact]
    public void CurrentPlayer_BankruptCurrent_ReturnsNullByDefault()
    {
        var game = CreateGame(currentPlayerId: C);
        Assert.Null(game.CurrentPlayer());
    }

    [Fact]
    public void CurrentPlayer_BankruptCurrentWithBankruptFilter_ReturnsCurrent()
    {
        var game = CreateGame(currentPlayerId: C);
        var player = game.CurrentPlayer(bankrupt: true);

        Assert.NotNull(player);
        Assert.Equal(C, player!.PlayerId);
    }


    // ─── GetPlayers (KEY METHOD) ────────────────────────────────────────

    [Fact]
    public void GetPlayers_DefaultPov_ExcludesPovAndBankrupt_OrderedClockwise()
    {
        // POV = current = A (OrderId 0). Active others clockwise: B, D. (C bankrupt.)
        var game = CreateGame(currentPlayerId: A);
        Assert.Equal(new[] { B, D }, Ids(game.GetPlayers()));
    }

    [Fact]
    public void GetPlayers_DefaultPov_IncludePov_StartsWithPov()
    {
        // POV = A first, then clockwise B, D.
        var game = CreateGame(currentPlayerId: A);
        Assert.Equal(new[] { A, B, D }, Ids(game.GetPlayers(excludePovPlayer: false)));
    }

    [Fact]
    public void GetPlayers_PovMidOrder_WrapsClockwise()
    {
        // POV = B (OrderId 1). After B: D (3). Wrap to before B: A (0).
        var game = CreateGame();
        Assert.Equal(new[] { D, A }, Ids(game.GetPlayers(B)));
    }

    [Fact]
    public void GetPlayers_PovHighestOrder_WrapsToStart()
    {
        // POV = D (OrderId 3, highest). No one after; wrap to A, B.
        var game = CreateGame();
        Assert.Equal(new[] { A, B }, Ids(game.GetPlayers(D)));
    }

    [Fact]
    public void GetPlayers_PovHighestOrder_IncludePov()
    {
        var game = CreateGame();
        Assert.Equal(new[] { D, A, B }, Ids(game.GetPlayers(D, excludePovPlayer: false)));
    }

    [Fact]
    public void GetPlayers_BankruptFilter_ReturnsOnlyBankrupt()
    {
        // Only C is bankrupt.
        var game = CreateGame();
        Assert.Equal(new[] { C }, Ids(game.GetPlayers(bankrupt: true)));
    }

    [Fact]
    public void GetPlayers_PovIsBankruptButFilterActive_ExcludesPovRegardless()
    {
        // POV = C (bankrupt) while asking for active rows. C cannot appear in the
        // active list even with excludePovPlayer:false; the rest order clockwise
        // from C's OrderId (2): after = D (3); wrap = A (0), B (1).
        var game = CreateGame();
        Assert.Equal(new[] { D, A, B }, Ids(game.GetPlayers(C, bankrupt: false, excludePovPlayer: false)));
    }

    [Fact]
    public void GetPlayers_UnknownPov_Throws()
    {
        var game = CreateGame();
        Assert.Throws<InvalidOperationException>(() => game.GetPlayers("ghost"));
    }

    [Theory]
    [InlineData(A, new[] { B, C, D })]
    [InlineData(B, new[] { C, D, A })]
    [InlineData(C, new[] { D, A, B })]
    [InlineData(D, new[] { A, B, C })]
    public void GetPlayers_FourActive_ReturnsClockwiseFromPov_RegardlessOfInsertionOrder(
        string pov, string[] expected)
    {
        // The §9 regression guard: a full clockwise rotation must hold even though
        // the underlying Players list is stored out of seat order.
        var game = CreateAllActiveGame();
        Assert.Equal(expected, Ids(game.GetPlayers(pov)));
    }


    // ─── CheckAnyDiceNumbers ────────────────────────────────────────────

    [Fact]
    public void CheckAnyDiceNumbers_MatchesActivePlayer_ReturnsThatPlayer()
    {
        // A's dice number is {2,5}; the roll is the same pair in reverse order.
        var game = CreateGame();
        Assert.Equal(A, game.CheckAnyDiceNumbers(new DiceRoll(5, 2)));
    }

    [Fact]
    public void CheckAnyDiceNumbers_MatchesOtherActivePlayer_ReturnsThatPlayer()
    {
        var game = CreateGame();
        Assert.Equal(B, game.CheckAnyDiceNumbers(new DiceRoll(3, 6)));
    }

    [Fact]
    public void CheckAnyDiceNumbers_MatchesBankruptPlayer_ReturnsNull()
    {
        // {5,1} is C's number, but C is bankrupt and excluded from the active scan.
        var game = CreateGame();
        Assert.Null(game.CheckAnyDiceNumbers(new DiceRoll(5, 1)));
    }

    [Fact]
    public void CheckAnyDiceNumbers_NoMatch_ReturnsNull()
    {
        var game = CreateGame();
        Assert.Null(game.CheckAnyDiceNumbers(new DiceRoll(1, 2)));
    }


    // ─── GetPropertySpace ───────────────────────────────────────────────

    [Fact]
    public void GetPropertySpace_OwnedProperty_ReturnsProperty()
    {
        var game = CreateGame();
        var prop = game.GetPropertySpace(1);

        Assert.NotNull(prop);
        Assert.Equal("Old Kent Road", prop!.Name);
        Assert.Equal(A, prop.OwnerPlayerId);
    }

    [Fact]
    public void GetPropertySpace_UnownedProperty_ReturnsPropertyWithNoOwner()
    {
        var game = CreateGame();
        var prop = game.GetPropertySpace(31);

        Assert.NotNull(prop);
        Assert.Null(prop!.OwnerPlayerId);
    }

    [Fact]
    public void GetPropertySpace_NonPropertySpace_ReturnsNull()
    {
        // Index 7 is a Chance space — no PropertyModel exists for it.
        var game = CreateGame();
        Assert.Null(game.GetPropertySpace(7));
    }


    // ─── GetOwnedProperties ─────────────────────────────────────────────

    [Fact]
    public void GetOwnedProperties_AllStatesIncludedByDefault()
    {
        // A owns 8: 1,3,6,8,9,5 (owned) + 37 (mortgaged) + 39 (reserved).
        var game = CreateGame();
        Assert.Equal(8, game.GetOwnedProperties(A).Count);
    }

    [Fact]
    public void GetOwnedProperties_CurrentPlayerOverload_UsesCurrentPlayer()
    {
        var game = CreateGame(currentPlayerId: A);
        Assert.Equal(8, game.GetOwnedProperties().Count);
    }

    [Fact]
    public void GetOwnedProperties_ExcludeMortgaged_DropsMortgaged()
    {
        var game = CreateGame();
        var owned = game.GetOwnedProperties(A, includeMortgaged: false);

        Assert.Equal(7, owned.Count);
        Assert.DoesNotContain(owned, p => p.BoardIndex == 37);
    }

    [Fact]
    public void GetOwnedProperties_ExcludeReserved_DropsReserved()
    {
        var game = CreateGame();
        var owned = game.GetOwnedProperties(A, includeReserved: false);

        Assert.Equal(7, owned.Count);
        Assert.DoesNotContain(owned, p => p.BoardIndex == 39);
    }

    [Fact]
    public void GetOwnedProperties_ExcludeMortgagedAndReserved_DropsBoth()
    {
        var game = CreateGame();
        var owned = game.GetOwnedProperties(A, includeMortgaged: false, includeReserved: false);

        Assert.Equal(6, owned.Count);
        Assert.DoesNotContain(owned, p => p.BoardIndex is 37 or 39);
    }

    [Fact]
    public void GetOwnedProperties_SetFilter_ReturnsOnlyThatSet()
    {
        var game = CreateGame();
        var blue = game.GetOwnedProperties(A, set: PropertySet.Blue);

        Assert.Equal(new ushort[] { 6, 8, 9 }, blue.Select(p => p.BoardIndex).OrderBy(i => i));
    }

    [Fact]
    public void GetOwnedProperties_StationSetFilter_ReturnsStation()
    {
        var game = CreateGame();
        var stations = game.GetOwnedProperties(A, set: PropertySet.Station);

        Assert.Single(stations);
        Assert.Equal(5, stations[0].BoardIndex);
    }

    [Fact]
    public void GetOwnedProperties_SetFilterCombinedWithStateFilter()
    {
        // DarkBlue = {37 mortgaged, 39 reserved}; excluding mortgaged leaves only 39.
        var game = CreateGame();
        var darkBlue = game.GetOwnedProperties(A, set: PropertySet.DarkBlue, includeMortgaged: false);

        Assert.Single(darkBlue);
        Assert.Equal(39, darkBlue[0].BoardIndex);
    }

    [Fact]
    public void GetOwnedProperties_PlayerOwnsNothing_ReturnsEmpty()
    {
        var game = CreateGame();
        Assert.Empty(game.GetOwnedProperties(C));
    }


    // ─── HasStreetEffect ────────────────────────────────────────────────

    [Fact]
    public void HasStreetEffect_OwnsFullStreet_ReturnsTrue()
    {
        // A owns the complete Brown+Blue street, all qualifying.
        var game = CreateGame();
        Assert.True(game.HasStreetEffect(A, PropertySet.Brown));
    }

    [Fact]
    public void HasStreetEffect_PartnerSetIsSymmetric()
    {
        // Querying the partner set (Blue) resolves the same street.
        var game = CreateGame();
        Assert.True(game.HasStreetEffect(A, PropertySet.Blue));
    }

    [Fact]
    public void HasStreetEffect_CurrentPlayerOverload_UsesCurrentPlayer()
    {
        var game = CreateGame(currentPlayerId: A);
        Assert.True(game.HasStreetEffect(PropertySet.Brown));
    }

    [Fact]
    public void HasStreetEffect_StreetContainsBuiltOnProperty_ReturnsFalse()
    {
        // B owns the full Red+Yellow street, but Fleet Street (23) is BuiltOn,
        // which is not a qualifying state.
        var game = CreateGame();
        Assert.False(game.HasStreetEffect(B, PropertySet.Red));
    }

    [Fact]
    public void HasStreetEffect_DoesNotOwnStreet_ReturnsFalse()
    {
        // A owns no Green properties.
        var game = CreateGame();
        Assert.False(game.HasStreetEffect(A, PropertySet.Green));
    }

    [Fact]
    public void HasStreetEffect_AbsoluteCheck_MortgagedBreaksStreet()
    {
        // D's Pink+Orange street has Northumberland Avenue (14) mortgaged.
        // Absolute check ignores mortgaged properties → street incomplete.
        var game = CreateGame();
        Assert.False(game.HasStreetEffect(D, PropertySet.Pink, absoluteCheck: true));
    }

    [Fact]
    public void HasStreetEffect_LenientCheck_MortgagedStillCounts()
    {
        // The lenient check includes the mortgaged property, so the street qualifies.
        var game = CreateGame();
        Assert.True(game.HasStreetEffect(D, PropertySet.Pink, absoluteCheck: false));
    }

    [Theory]
    [InlineData(PropertySet.Station)]
    [InlineData(PropertySet.Utility)]
    public void HasStreetEffect_StationOrUtility_AlwaysFalse(PropertySet set)
    {
        // Stations and utilities have no street effect — they aren't street partners.
        var game = CreateGame();
        Assert.False(game.HasStreetEffect(A, set));
        Assert.False(game.HasStreetEffect(B, set));
    }
}