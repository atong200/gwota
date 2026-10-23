namespace Gwota.Core.Tests.Support;

/// <summary>
/// Builds a mid-match state (phase Playing, seat One to act) in an engine. By default each player holds a filler
/// card so the round does not auto-end, and leaders are marked used unless configured.
/// </summary>
internal sealed class Scenario
{
    public static readonly CardDefinition FillerCard = TestCards.Unit(1, RowType.Siege, "filler");

    public Scenario(
        Faction factionOne = Faction.Neutral,
        Faction factionTwo = Faction.Neutral,
        LeaderDefinition? leaderOne = null,
        LeaderDefinition? leaderTwo = null,
        bool withFillers = true)
    {
        State = new MatchState(
            CreatePlayer(Seat.One, factionOne, leaderOne),
            CreatePlayer(Seat.Two, factionTwo, leaderTwo))
        {
            Phase = MatchPhase.Playing,
            ActiveSeat = Seat.One,
            RoundStarter = Seat.One,
        };

        if (withFillers)
        {
            AddToHand(Seat.One, FillerCard);
            AddToHand(Seat.Two, FillerCard);
        }
    }

    public MatchState State { get; }

    public PlayerState One => State.Player(Seat.One);

    public PlayerState Two => State.Player(Seat.Two);

    public CardInstance AddToHand(Seat seat, CardDefinition definition) => Add(State.Player(seat).HandList, definition, seat);

    public CardInstance AddToDeck(Seat seat, CardDefinition definition) => Add(State.Player(seat).DeckList, definition, seat);

    public CardInstance AddToDiscard(Seat seat, CardDefinition definition) => Add(State.Player(seat).DiscardList, definition, seat);

    /// <summary>Puts a card on <paramref name="side"/>'s row; <paramref name="owner"/> defaults to the side (differs for spies).</summary>
    public CardInstance AddToBoard(Seat side, RowType row, CardDefinition definition, Seat? owner = null) =>
        Add(State.Player(side).Row(row).CardList, definition, owner ?? side);

    public CardInstance AddWeather(CardDefinition definition, Seat owner = Seat.One) => Add(State.WeatherList, definition, owner);

    public CardInstance SetHorn(Seat side, RowType row, CardDefinition? horn = null)
    {
        var card = NewCard(horn ?? TestCards.Horn(), side);
        State.Player(side).Row(row).HornCard = card;
        return card;
    }

    public MatchEngine Build(ulong seed = 1, MatchRules? rules = null) => MatchEngine.FromState(State, new SeededRandom(seed), rules);

    private static PlayerState CreatePlayer(Seat seat, Faction faction, LeaderDefinition? leader) =>
        new(seat, $"Player {seat}", faction, leader ?? TestCards.Leader(LeaderAbility.ClearWeather), gems: 2)
        {
            LeaderUsed = leader is null,
            HasFinishedDraft = true,
        };

    private CardInstance Add(List<CardInstance> list, CardDefinition definition, Seat owner)
    {
        var card = NewCard(definition, owner);
        list.Add(card);
        return card;
    }

    private CardInstance NewCard(CardDefinition definition, Seat owner) => new(State.NextInstanceId++, definition, owner);
}
