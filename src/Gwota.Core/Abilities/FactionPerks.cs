namespace Gwota.Core.Abilities;

/// <summary>A passive faction bonus hooked into the end of each round.</summary>
internal interface IFactionPerk
{
    Faction Faction { get; }

    /// <summary>Player-facing description of the perk.</summary>
    string Description { get; }

    /// <summary>Whether a drawn round counts as a win for this faction.</summary>
    bool WinsTies => false;

    /// <summary>Units that stay on the board when it is cleared (only called if the match continues).</summary>
    IReadOnlyList<CardInstance> SelectUnitsToKeep(MatchContext context, Seat seat) => [];

    /// <summary>Called after the board is cleared, when the match continues.</summary>
    void OnRoundEnded(MatchContext context, Seat seat, RoundResult result)
    {
    }
}

public static class FactionPerks
{
    private static readonly Dictionary<Faction, IFactionPerk> Perks = new IFactionPerk[]
    {
        new RealmsPerk(),
        new AbyssPerk(),
        new HordePerk(),
    }.ToDictionary(p => p.Faction);

    /// <summary>Player-facing description of a faction's perk.</summary>
    public static string Describe(Faction faction) => Find(faction)?.Description ?? "No perk.";

    /// <summary>The seat that wins a drawn round, if exactly one player's faction wins ties.</summary>
    public static Seat? TieWinner(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var holders = state.Players.Where(p => Find(p.Faction)?.WinsTies == true).ToList();
        return holders.Count == 1 ? holders[0].Seat : null;
    }

    internal static IFactionPerk? Find(Faction faction) => Perks.GetValueOrDefault(faction);
}

/// <summary>United Realms: draws a card after winning a round.</summary>
internal sealed class RealmsPerk : IFactionPerk
{
    public Faction Faction => Faction.Realms;

    public string Description => "Draw a card after winning a round.";

    public void OnRoundEnded(MatchContext context, Seat seat, RoundResult result)
    {
        if (result.Winner == seat && context.Draw(seat, 1).Count > 0)
        {
            context.Emit(new PerkTriggered(seat, Faction, Description));
        }
    }
}

/// <summary>Abyssal Legion: one random unit stays on the board after each round.</summary>
internal sealed class AbyssPerk : IFactionPerk
{
    public Faction Faction => Faction.Abyss;

    public string Description => "One random unit stays on the board after each round.";

    public IReadOnlyList<CardInstance> SelectUnitsToKeep(MatchContext context, Seat seat)
    {
        var units = context.Player(seat).Rows.SelectMany(r => r.Cards).Where(c => c.Definition.IsUnit).ToList();
        if (units.Count == 0)
        {
            return [];
        }

        var kept = context.Random.Pick(units);
        context.Emit(new PerkTriggered(seat, Faction, $"{kept.Definition.Name} stays on the board."));
        return [kept];
    }
}

/// <summary>Wild Horde: wins drawn rounds, unless the opponent is Horde too.</summary>
internal sealed class HordePerk : IFactionPerk
{
    public const string TieText = "The tie goes to the Horde.";

    public Faction Faction => Faction.Horde;

    public string Description => "Win the round when the scores are tied.";

    public bool WinsTies => true;
}
