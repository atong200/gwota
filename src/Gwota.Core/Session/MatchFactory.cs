using Gwota.Core.Ai;
using Gwota.Core.Data;

namespace Gwota.Core.Session;

/// <summary>
/// The decks, player names and seed of a match, kept so it can be replayed or rematched against the same bot.
/// Names are display names only: a deck's faction is not its player's identity (mirror matches are possible).
/// </summary>
public sealed record MatchSetup(
    DeckDefinition PlayerDeck,
    DeckDefinition OpponentDeck,
    ulong Seed,
    string PlayerName = MatchSetup.DefaultPlayerName,
    string OpponentName = MatchSetup.DefaultOpponentName)
{
    public const string DefaultPlayerName = "Player One";
    public const string DefaultOpponentName = "Player Two";
}

/// <summary>Builds matches from loaded <see cref="GameData"/>. Against the AI, the human sits in <see cref="Seat.One"/>.</summary>
public static class MatchFactory
{
    public const Seat HumanSeat = Seat.One;
    public const Seat AiSeat = Seat.Two;

    /// <summary>Shipped decks (the bots' pool), ordered for display (by faction, then name).</summary>
    public static IReadOnlyList<DeckDefinition> PlayableDecks(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Decks.Values.OrderBy(d => d.Faction).ThenBy(d => d.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Picks an opponent deck, preferring the other factions; falls back to any deck (even the same one).</summary>
    public static DeckDefinition PickOpponent(GameData data, DeckDefinition playerDeck, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(playerDeck);
        ArgumentNullException.ThrowIfNull(random);

        var decks = PlayableDecks(data);
        var rivals = decks.Where(d => d.Faction != playerDeck.Faction).ToList();
        return random.Pick(rivals.Count > 0 ? rivals : decks);
    }

    /// <summary>
    /// A human-vs-AI setup: picks the opponent deck and a bot name distinct from <paramref name="playerName"/>.
    /// </summary>
    public static MatchSetup VersusAiSetup(GameData data, DeckDefinition playerDeck, string playerName, IRandom random, ulong seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var opponent = PickOpponent(data, playerDeck, random);
        return new MatchSetup(playerDeck, opponent, seed, playerName, BotNames.Default.Pick(random, [playerName]));
    }

    /// <summary>An AI-vs-AI setup (demo mode) with two distinct bot names.</summary>
    public static MatchSetup AiVersusAiSetup(DeckDefinition deckOne, DeckDefinition deckTwo, IRandom random, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(random);

        var one = BotNames.Default.Pick(random);
        return new MatchSetup(deckOne, deckTwo, seed, one, BotNames.Default.Pick(random, [one]));
    }

    /// <summary>Creates a human-vs-AI session with the names of <paramref name="setup"/>.</summary>
    public static MatchSession VersusAi(GameData data, MatchSetup setup, IPlayerController? ai = null, MatchRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(setup);

        var engine = MatchEngine.Create(
            data.CreatePlayerSetup(setup.PlayerDeck, setup.PlayerName),
            data.CreatePlayerSetup(setup.OpponentDeck, setup.OpponentName),
            new SeededRandom(setup.Seed),
            rules);
        return new MatchSession(engine, new Dictionary<Seat, IPlayerController> { [AiSeat] = ai ?? new HeuristicAi() });
    }

    /// <summary>Creates a session between two humans: <see cref="MatchSetup.PlayerDeck"/> sits in <see cref="Seat.One"/>.</summary>
    public static MatchSession VersusPlayer(GameData data, MatchSetup setup, MatchRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(setup);

        var engine = MatchEngine.Create(
            data.CreatePlayerSetup(setup.PlayerDeck, setup.PlayerName),
            data.CreatePlayerSetup(setup.OpponentDeck, setup.OpponentName),
            new SeededRandom(setup.Seed),
            rules);
        return new MatchSession(engine, new Dictionary<Seat, IPlayerController>());
    }

    /// <summary>Creates an AI-vs-AI session (smoke tests, balance runs).</summary>
    public static MatchSession AiVersusAi(GameData data, MatchSetup setup, MatchRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(setup);

        var engine = MatchEngine.Create(
            data.CreatePlayerSetup(setup.PlayerDeck, setup.PlayerName),
            data.CreatePlayerSetup(setup.OpponentDeck, setup.OpponentName),
            new SeededRandom(setup.Seed),
            rules);
        return new MatchSession(engine, new Dictionary<Seat, IPlayerController>
        {
            [Seat.One] = new HeuristicAi(),
            [Seat.Two] = new HeuristicAi(),
        });
    }
}
