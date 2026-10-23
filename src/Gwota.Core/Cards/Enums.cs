namespace Gwota.Core.Cards;

public enum Faction
{
    Neutral,
    Realms,
    Hells,
}

/// <summary>The three combat rows.</summary>
public enum RowType
{
    Melee,
    Ranged,
    Siege,
}

public enum CardKind
{
    /// <summary>A unit placed on a combat row, contributing its strength.</summary>
    Unit,

    /// <summary>A one-shot or row effect (horn, scorch, decoy, weather, clear weather).</summary>
    Special,
}

/// <summary>Abilities carried by unit cards.</summary>
public enum UnitAbility
{
    /// <summary>Placed on the opponent's side; its player draws two cards.</summary>
    Spy,

    /// <summary>Revives a non-hero unit from its player's discard pile.</summary>
    Medic,

    /// <summary>Plays every card of the same muster group from the deck.</summary>
    Muster,

    /// <summary>Strength is multiplied by the number of copies of this card in the row.</summary>
    TightBond,

    /// <summary>Adds +1 strength to every other unit in its row.</summary>
    MoraleBoost,

    /// <summary>Doubles the strength of every other unit in its row (does not stack with a horn).</summary>
    Horn,

    /// <summary>Destroys the strongest enemy unit(s) in the matching row if that row totals at least the scorch threshold.</summary>
    ScorchRow,
}

/// <summary>Effects carried by special cards.</summary>
public enum SpecialEffect
{
    /// <summary>Doubles the strength of non-hero units in one of its player's rows.</summary>
    Horn,

    /// <summary>Destroys the strongest non-hero unit(s) on the whole battlefield.</summary>
    Scorch,

    /// <summary>Swaps with a non-hero unit on its player's side, returning that unit to hand.</summary>
    Decoy,

    /// <summary>Reduces non-hero units in the affected rows (both sides) to 1 strength.</summary>
    Weather,

    /// <summary>Removes every active weather effect.</summary>
    ClearWeather,
}

public enum LeaderAbility
{
    /// <summary>Removes every active weather effect.</summary>
    ClearWeather,

    /// <summary>Plays a weather card affecting the leader's row straight from the deck.</summary>
    PlayWeatherFromDeck,

    /// <summary>Applies a commander's horn to the leader's row (if it has none).</summary>
    HornRow,

    /// <summary>Scorches the opponent's matching row if it totals at least the scorch threshold.</summary>
    ScorchRow,

    /// <summary>Returns a chosen card from the discard pile to the hand.</summary>
    ReturnFromDiscard,
}
