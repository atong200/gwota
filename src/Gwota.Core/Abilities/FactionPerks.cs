namespace Gwota.Core.Abilities;

/// <summary>A passive faction bonus hooked into the end of each round.</summary>
internal interface IFactionPerk
{
    Faction Faction { get; }

    /// <summary>Units that stay on the board when it is cleared (only called if the match continues).</summary>
    IReadOnlyList<CardInstance> SelectUnitsToKeep(MatchContext context, Seat seat);

    /// <summary>Called after the board is cleared, when the match continues.</summary>
    void OnRoundEnded(MatchContext context, Seat seat, RoundResult result);
}

public static class FactionPerks
{
    private static readonly Dictionary<Faction, IFactionPerk> Perks = new IFactionPerk[]
    {
        new RealmsPerk(),
        new HellsPerk(),
    }.ToDictionary(p => p.Faction);

    /// <summary>Player-facing description of a faction's perk.</summary>
    public static string Describe(Faction faction) => faction switch
    {
        Faction.Realms => RealmsPerk.Description,
        Faction.Hells => HellsPerk.Description,
        _ => "No perk.",
    };

    internal static IFactionPerk? Find(Faction faction) => Perks.GetValueOrDefault(faction);
}

/// <summary>Realms of Men: draws a card after winning a round.</summary>
internal sealed class RealmsPerk : IFactionPerk
{
    public const string Description = "Draw a card after winning a round.";

    public Faction Faction => Faction.Realms;

    public IReadOnlyList<CardInstance> SelectUnitsToKeep(MatchContext context, Seat seat) => [];

    public void OnRoundEnded(MatchContext context, Seat seat, RoundResult result)
    {
        if (result.Winner != seat)
        {
            return;
        }

        if (context.Draw(seat, 1).Count > 0)
        {
            context.Emit(new PerkTriggered(seat, Faction, Description));
        }
    }
}

/// <summary>The Seven Hells: one random unit stays on the board after each round.</summary>
internal sealed class HellsPerk : IFactionPerk
{
    public const string Description = "One random unit stays on the board after each round.";

    public Faction Faction => Faction.Hells;

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

    public void OnRoundEnded(MatchContext context, Seat seat, RoundResult result)
    {
    }
}
