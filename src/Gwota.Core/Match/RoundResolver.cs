namespace Gwota.Core.Match;

/// <summary>
/// Ends a round: scores it, takes gems, keeps perk-selected units, sweeps the board, runs end-of-round perks and
/// decides whether the match is over. Stateless; everything lives in the <see cref="MatchContext"/>.
/// </summary>
internal static class RoundResolver
{
    /// <summary>Resolves the current round. Returns the next round's starter, or null when the match is over.</summary>
    public static Seat? EndRound(MatchContext context)
    {
        var state = context.State;
        var scoreOne = StrengthCalculator.PlayerTotal(state, Seat.One);
        var scoreTwo = StrengthCalculator.PlayerTotal(state, Seat.Two);
        Seat? winner = scoreOne > scoreTwo ? Seat.One : scoreTwo > scoreOne ? Seat.Two : BreakTie(context);

        if (winner is { } roundWinner)
        {
            state.Player(roundWinner.Opponent()).Gems--;
        }
        else
        {
            state.Player(Seat.One).Gems--;
            state.Player(Seat.Two).Gems--;
        }

        var result = new RoundResult(state.Round, scoreOne, scoreTwo, winner);
        state.RoundResultList.Add(result);
        context.Emit(new RoundEnded(result));

        var matchOver = state.Players.Any(p => p.Gems <= 0);
        HashSet<int> kept = matchOver ? [] : SelectUnitsToKeep(context);
        ClearBoard(context, kept);

        if (matchOver)
        {
            FinishMatch(context);
            return null;
        }

        foreach (var player in state.Players)
        {
            FactionPerks.Find(player.Faction)?.OnRoundEnded(context, player.Seat, result);
        }

        state.Round++;
        return NextStarter(context, result);
    }

    private static Seat? BreakTie(MatchContext context)
    {
        if (FactionPerks.TieWinner(context.State) is not { } seat)
        {
            return null;
        }

        context.Emit(new PerkTriggered(seat, context.Player(seat).Faction, HordePerk.TieText));
        return seat;
    }

    /// <summary>Perks pick in seat order; the order of random draws must not change.</summary>
    private static HashSet<int> SelectUnitsToKeep(MatchContext context)
    {
        var kept = new HashSet<int>();
        foreach (var player in context.State.Players)
        {
            if (FactionPerks.Find(player.Faction) is { } perk)
            {
                kept.UnionWith(perk.SelectUnitsToKeep(context, player.Seat).Select(c => c.Id));
            }
        }

        return kept;
    }

    private static void ClearBoard(MatchContext context, HashSet<int> keptIds)
    {
        foreach (var player in context.State.Players)
        {
            foreach (var row in player.Rows)
            {
                foreach (var card in row.CardList.Where(c => !keptIds.Contains(c.Id)).ToList())
                {
                    row.CardList.Remove(card);
                    context.Discard(card);
                }

                if (row.HornCard is { } horn)
                {
                    context.Discard(horn);
                    row.HornCard = null;
                }

                row.HasLeaderHorn = false;
            }
        }

        context.ClearWeather();
    }

    private static void FinishMatch(MatchContext context)
    {
        var state = context.State;
        var alive = state.Players.Where(p => p.Gems > 0).ToList();
        state.Outcome = new MatchOutcome(alive.Count == 1 ? alive[0].Seat : null);
        state.Phase = MatchPhase.Finished;
        context.Emit(new MatchEnded(state.Outcome));
    }

    private static Seat NextStarter(MatchContext context, RoundResult result)
    {
        if (result.Winner is not { } winner)
        {
            return context.State.RoundStarter.Opponent();
        }

        return context.Rules.NextRoundStarter == NextRoundStarter.RoundWinner ? winner : winner.Opponent();
    }
}
