namespace Gwota.Core.Match;

/// <summary>
/// Enumerates every legal command for a seat. This is the single source of truth for legality: the UI highlights
/// from it and the AI picks from it; the engine validates with <see cref="IsLegal"/>, which tests keep equal to
/// <c>For(...).Contains</c>.
/// </summary>
public static class LegalCommands
{
    public static IReadOnlyList<GameCommand> For(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase switch
        {
            MatchPhase.Draft => DraftCommands(state.Player(seat)),
            MatchPhase.Playing => PlayingCommands(state, seat).ToList(),
            _ => [],
        };
    }

    /// <summary>True when the seat could do something other than passing. Stops at the first such command.</summary>
    public static bool HasActionBesidesPass(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase == MatchPhase.Playing && PlayingCommands(state, seat).Any(c => c is not PassCommand);
    }

    /// <summary>
    /// Same answer as <c>For(state, command.Seat).Contains(command)</c>, without enumerating every legal command.
    /// </summary>
    public static bool IsLegal(MatchState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        var seat = command.Seat;
        var player = state.Player(seat);
        if (state.Phase == MatchPhase.Draft)
        {
            return command is FinishDraftCommand draft && IsLegalDraft(player, draft);
        }

        if (state.Phase != MatchPhase.Playing)
        {
            return false;
        }

        if (state.PendingChoice is { } choice)
        {
            return command is ResolveChoiceCommand resolve && choice.Seat == seat && choice.Options.Contains(resolve.CardId);
        }

        if (state.ActiveSeat != seat || player.HasPassed)
        {
            return false;
        }

        return command switch
        {
            PassCommand => true,
            PlayCardCommand play => player.FindInHand(play.CardId) is { } card
                && PlaysFor(state, seat, card).Contains(play),
            UseLeaderCommand use => !player.LeaderUsed
                && LeaderAbilities.Get(player.Leader.Ability).LegalUses(state, seat, player.Leader).Contains(use),
            _ => false,
        };
    }

    private static bool IsLegalDraft(PlayerState player, FinishDraftCommand draft)
    {
        if (player.HasFinishedDraft)
        {
            return false;
        }

        var ids = draft.RedrawnCardIds;
        var maxRedraws = Math.Min(player.RedrawLimit, Math.Min(player.Deck.Count, player.Hand.Count));
        if (ids.Count > maxRedraws)
        {
            return false;
        }

        for (var i = 0; i < ids.Count; i++)
        {
            if (player.FindInHand(ids[i]) is null)
            {
                return false;
            }

            for (var j = 0; j < i; j++)
            {
                if (ids[j] == ids[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

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

    /// <summary>Lazy, so callers looking for a single command stop early. Enumerate before mutating the state.</summary>
    private static IEnumerable<GameCommand> PlayingCommands(MatchState state, Seat seat)
    {
        if (state.PendingChoice is { } choice)
        {
            if (choice.Seat == seat)
            {
                foreach (var id in choice.Options)
                {
                    yield return new ResolveChoiceCommand(seat, id);
                }
            }

            yield break;
        }

        var player = state.Player(seat);
        if (state.ActiveSeat != seat || player.HasPassed)
        {
            yield break;
        }

        foreach (var card in player.Hand)
        {
            foreach (var play in PlaysFor(state, seat, card))
            {
                yield return play;
            }
        }

        if (!player.LeaderUsed)
        {
            foreach (var use in LeaderAbilities.Get(player.Leader.Ability).LegalUses(state, seat, player.Leader))
            {
                yield return use;
            }
        }

        yield return new PassCommand(seat);
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
