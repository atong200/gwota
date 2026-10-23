namespace Gwota.Core.Data;

/// <summary>Every card, leader and deck loaded from the data folder.</summary>
public sealed class GameData
{
    public GameData(
        IReadOnlyDictionary<string, CardDefinition> cards,
        IReadOnlyDictionary<string, LeaderDefinition> leaders,
        IReadOnlyDictionary<string, DeckDefinition> decks)
    {
        Cards = cards;
        Leaders = leaders;
        Decks = decks;
    }

    public IReadOnlyDictionary<string, CardDefinition> Cards { get; }

    public IReadOnlyDictionary<string, LeaderDefinition> Leaders { get; }

    public IReadOnlyDictionary<string, DeckDefinition> Decks { get; }

    public IReadOnlyList<CardDefinition> ExpandDeck(DeckDefinition deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return deck.Cards.SelectMany(entry => Enumerable.Repeat(Cards[entry.CardId], entry.Count)).ToList();
    }

    public PlayerSetup CreatePlayerSetup(string deckId, string playerName)
    {
        if (!Decks.TryGetValue(deckId, out var deck))
        {
            throw new KeyNotFoundException($"Unknown deck '{deckId}'.");
        }

        return CreatePlayerSetup(deck, playerName);
    }

    /// <summary>A match setup from any deck (shipped or built by the player) whose cards and leader are known.</summary>
    public PlayerSetup CreatePlayerSetup(DeckDefinition deck, string playerName)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return new PlayerSetup(playerName, deck.Faction, Leaders[deck.LeaderId], ExpandDeck(deck));
    }
}

public sealed class GameDataException : Exception
{
    public GameDataException()
        : this([])
    {
    }

    public GameDataException(string message)
        : this([message])
    {
    }

    public GameDataException(string message, Exception innerException)
        : base(message, innerException) => Errors = [message];

    public GameDataException(IReadOnlyList<string> errors)
        : base("Invalid game data:" + Environment.NewLine + string.Join(Environment.NewLine, errors)) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}
