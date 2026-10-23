namespace Gwota.Core.Match;

/// <summary>
/// Enumerates every legal command for a seat. This is the single source of truth for legality:
/// the engine rejects anything not listed here, the UI highlights from it and the AI picks from it.
/// </summary>
public static class LegalCommands
{
    public static IReadOnlyList<GameCommand> For(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase switch
        {
            MatchPhase.Draft => DraftCommands(state.Player(seat)),
            MatchPhase.Playing => PlayingCommands(state, seat),
            _ => [],
        };
    }

    /// <summary>True when the seat could do something other than passing.</summary>
    public static bool HasActionBesidesPass(MatchState state, Seat seat) =>
        For(state, seat).Any(c => c is not PassCommand);

    /// <summary>One command per selection of at most <see cref="PlayerState.RedrawLimit"/> hand cards (bounded by the deck size).</summary>
    private static List<GameCommand> DraftCommands(PlayerState player)
    {
        if (player.HasFinishedDraft)
        {
            return [];
        }

        var maxRedraws = Math.Min(player.RedrawLimit, Math.Min(player.Deck.Count, player.Hand.Count));
        var handIds = player.Hand.Select(c => c.Id).ToList();
        return Selections(handIds, 0, maxRedraws)
            .Select(ids => (GameCommand)new FinishDraftCommand(player.Seat, ids))
            .ToList();
    }

    /// <summary>Every subset of <paramref name="ids"/> (from <paramref name="start"/>) with at most <paramref name="max"/> items, in hand order.</summary>
    private static IEnumerable<int[]> Selections(IReadOnlyList<int> ids, int start, int max)
    {
        yield return [];
        if (max == 0)
        {
            yield break;
        }

        for (var i = start; i < ids.Count; i++)
        {
            foreach (var rest in Selections(ids, i + 1, max - 1))
            {
                yield return [ids[i], .. rest];
            }
        }
    }

    private static List<GameCommand> PlayingCommands(MatchState state, Seat seat)
    {
        if (state.PendingChoice is { } choice)
        {
            return choice.Seat == seat
                ? choice.Options.Select(id => (GameCommand)new ResolveChoiceCommand(seat, id)).ToList()
                : [];
        }

        var player = state.Player(seat);
        if (state.ActiveSeat != seat || player.HasPassed)
        {
            return [];
        }

        var commands = new List<GameCommand>();
        foreach (var card in player.Hand)
        {
            commands.AddRange(PlaysFor(state, seat, card));
        }

        if (!player.LeaderUsed && LeaderAbilities.Get(player.Leader.Ability).CanUse(state, seat, player.Leader))
        {
            commands.Add(new UseLeaderCommand(seat));
        }

        commands.Add(new PassCommand(seat));
        return commands;
    }

    private static IEnumerable<PlayCardCommand> PlaysFor(MatchState state, Seat seat, CardInstance card)
    {
        var definition = card.Definition;
        if (definition.IsUnit)
        {
            return definition.Rows.Select(row => new PlayCardCommand(seat, card.Id, row));
        }

        return definition.Effect is { } effect
            ? SpecialEffects.Get(effect).LegalPlays(state, seat, card)
            : [];
    }
}
